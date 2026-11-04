#include <windows.h>
#include <xaudio2.h>
#include <array>
#include <atomic>
#include <chrono>
#include <rex/audio/audio_driver.h>
#include <rex/audio/audio_system.h>
#include <algorithm>
#include <rex/audio/downmix.h>
#include <rex/audio/flags.h>
#include <rex/chrono/clock.h>
#include <rex/logging.h>
#include "windows_audio.h"
#include "windows_audio_com.h"
REXCVAR_DEFINE_BOOL(windows_audio, false, "Audio", "Use the native Windows XAudio2 output instead of SDL");
REXCVAR_DECLARE(bool, trace_audio_frames);
REXCVAR_DEFINE_INT32(game_volume, 100, "Audio", "Windows audio master volume, 0 to 100");
namespace {
using rex::X_STATUS;
class WindowsAudioDriver final : public rex::audio::AudioDriver, private IXAudio2VoiceCallback {
 struct Frame {std::array<float,512> pcm{};std::atomic<bool> busy{false};};
 std::array<Frame,64> frames_{};
 rex::thread::Semaphore* semaphore_;
 WindowsAudioCom com_;
 IXAudio2* engine_=nullptr;
 IXAudio2MasteringVoice* master_=nullptr;
 IXAudio2SourceVoice* voice_=nullptr;
 unsigned next_=0,submitted_=0,reports_=0;
 bool started_=false;
 std::atomic<unsigned> starved_passes_{0},callback_errors_{0};
 std::chrono::steady_clock::time_point last_{};
 std::uint64_t last_samples_=0,last_guest_=0;
 void STDMETHODCALLTYPE OnVoiceProcessingPassStart(UINT32 required) noexcept override {
  if(required)starved_passes_.fetch_add(1,std::memory_order_relaxed);
 }
 void STDMETHODCALLTYPE OnVoiceProcessingPassEnd() noexcept override {}
 void STDMETHODCALLTYPE OnStreamEnd() noexcept override {}
 void STDMETHODCALLTYPE OnBufferStart(void*) noexcept override {}
 void STDMETHODCALLTYPE OnBufferEnd(void* context) noexcept override {
  static_cast<Frame*>(context)->busy.store(false,std::memory_order_release);
  if(!semaphore_->Release(1,nullptr))callback_errors_.fetch_add(1,std::memory_order_relaxed);
 }
 void STDMETHODCALLTYPE OnLoopEnd(void*) noexcept override {}
 void STDMETHODCALLTYPE OnVoiceError(void*,HRESULT) noexcept override {callback_errors_.fetch_add(1,std::memory_order_relaxed);}
 public:
 WindowsAudioDriver(rex::memory::Memory* memory,rex::thread::Semaphore* sem):AudioDriver(memory),semaphore_(sem){}
 ~WindowsAudioDriver() noexcept override {
  if(voice_)voice_->DestroyVoice();
  if(master_)master_->DestroyVoice();
  if(engine_)engine_->Release();
 }
 bool Initialize(){
  const char* stage="COM MTA lifetime";
  HRESULT hr=com_.Initialize();
  if(SUCCEEDED(hr)){stage="XAudio2Create";hr=XAudio2Create(&engine_,0,XAUDIO2_DEFAULT_PROCESSOR);}
  if(SUCCEEDED(hr)){stage="CreateMasteringVoice (default Windows output)";hr=engine_->CreateMasteringVoice(&master_,2,48000);}
  WAVEFORMATEX format{};format.wFormatTag=WAVE_FORMAT_IEEE_FLOAT;format.nChannels=2;format.nSamplesPerSec=48000;format.wBitsPerSample=32;format.nBlockAlign=8;format.nAvgBytesPerSec=384000;
  if(SUCCEEDED(hr)){stage="CreateSourceVoice";hr=engine_->CreateSourceVoice(&voice_,&format,XAUDIO2_VOICE_NOPITCH,1.0f,this);}
  if(FAILED(hr)){REXLOG_ERROR("Windows audio initialization failed at {}: {:08X}",stage,static_cast<unsigned>(hr));return false;}
  master_->SetVolume(std::clamp(REXCVAR_GET(game_volume),0,100)/100.0f);
  REXLOG_INFO("Windows XAudio2 output: stereo float32, 48000 Hz, 256 samples/frame, fixed playback ratio 1.0");
  return true;
 }
 void SubmitFrame(std::uint32_t ptr) noexcept override {
  Frame* frame=nullptr;
  for(unsigned n=0;n<frames_.size();n++){
   auto& candidate=frames_[next_++%frames_.size()];bool expected=false;
   if(candidate.busy.compare_exchange_strong(expected,true,std::memory_order_acquire)){frame=&candidate;break;}
  }
  if(!frame)REX_FATAL("Windows audio buffer pool exhausted");
  if(REXCVAR_GET(audio_mute))frame->pcm.fill(0);
  else {
   const auto* pcm=memory_->TranslateVirtual<rex::be<float>*>(ptr);const auto fold=rex::audio::GetStereoFold();const float scale=fold.scale*rex::audio::GetOutputGain();
   for(unsigned n=0;n<256;n++){const float mid=float(pcm[512+n])*fold.center+float(pcm[768+n])*fold.lfe;frame->pcm[n*2]=std::clamp((float(pcm[n])+mid+float(pcm[1024+n])*fold.surround)*scale,-1.0f,1.0f);frame->pcm[n*2+1]=std::clamp((float(pcm[256+n])+mid+float(pcm[1280+n])*fold.surround)*scale,-1.0f,1.0f);}
  }
  XAUDIO2_BUFFER buffer{};buffer.AudioBytes=sizeof(frame->pcm);buffer.pAudioData=reinterpret_cast<const BYTE*>(frame->pcm.data());buffer.pContext=frame;
  const HRESULT hr=voice_->SubmitSourceBuffer(&buffer);
  if(FAILED(hr))REX_FATAL("Windows audio submission failed: {:08X}",static_cast<unsigned>(hr));
  if(!started_ && ++submitted_>=4){
   if(FAILED(voice_->Start()))REX_FATAL("Windows audio voice could not start");
   started_=true;last_=std::chrono::steady_clock::now();last_guest_=rex::chrono::Clock::QueryGuestTickCount();
  }
  if(started_ && REXCVAR_GET(trace_audio_frames) && reports_<600){
   const auto now=std::chrono::steady_clock::now();const double elapsed=std::chrono::duration<double>(now-last_).count();
   if(elapsed>=1){
    XAUDIO2_VOICE_STATE state{};voice_->GetState(&state);
    XAUDIO2_PERFORMANCE_DATA perf{};engine_->GetPerformanceData(&perf);
    const auto guest=rex::chrono::Clock::QueryGuestTickCount();
    REXLOG_INFO("WIN-AUDIO played_hz={:.0f} queued={} starved_passes={} engine_glitches={} callback_errors={} guest_clock_ratio={:.6f}",double(state.SamplesPlayed-last_samples_)/elapsed,state.BuffersQueued,starved_passes_.exchange(0),perf.GlitchesSinceEngineStarted,callback_errors_.load(),double(guest-last_guest_)/rex::chrono::Clock::guest_tick_frequency()/elapsed);
    last_=now;last_samples_=state.SamplesPlayed;last_guest_=guest;++reports_;
   }
  }
 }
};
std::unique_ptr<WindowsAudioDriver> CreateWindowsAudioDriver(rex::memory::Memory* memory,rex::thread::Semaphore* sem) {
 auto driver=std::make_unique<WindowsAudioDriver>(memory,sem);
 // The guest assumes registration succeeded and dereferences a null handle
 // after an audio failure. Stop here with the original host error in the log.
 if(!driver->Initialize())REX_FATAL("Windows audio output could not start. Check the default Windows playback device; the failing stage and error code are logged above.");
 return driver;
}
class WindowsAudioSystem final : public rex::audio::AudioSystem {
 public:
 explicit WindowsAudioSystem(rex::runtime::FunctionDispatcher* dispatcher):AudioSystem(dispatcher){}
 X_STATUS CreateDriver(size_t,rex::thread::Semaphore* sem,rex::audio::AudioDriver** out) noexcept override {
  *out=nullptr;
  auto driver=CreateWindowsAudioDriver(memory_,sem);
  *out=driver.release();return X_STATUS_SUCCESS;
 }
 void DestroyDriver(rex::audio::AudioDriver* driver) noexcept override {delete driver;}
};
}
void ConfigureWindowsAudio(rex::RuntimeConfig& config){
 if(REXCVAR_GET(windows_audio))config.audio_factory=[](rex::runtime::FunctionDispatcher* dispatcher)->std::unique_ptr<rex::system::IAudioSystem>{return std::make_unique<WindowsAudioSystem>(dispatcher);};
}
