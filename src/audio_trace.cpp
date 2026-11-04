#include <windows.h>

#include <array>
#include <algorithm>
#include <chrono>
#include <cmath>
#include <mutex>
#include <rex/hook.h>
#include <rex/cvar.h>
REXCVAR_DEFINE_BOOL(trace_audio_frames, false, "Debug", "Bounded audio PCM level and cadence diagnostics");
namespace {
using Clock=std::chrono::steady_clock;
struct AudioStats {Clock::time_point start{},last{};unsigned frames=0,bad=0,clipped=0,repeated=0,reports=0;double max_gap=0;std::uint64_t hash=0;std::array<float,6> peak{};std::array<double,6> power{};};
std::array<AudioStats,16> states;
std::mutex stats_mutex;
void Inspect(std::uint8_t*base,std::uint32_t ptr,unsigned client){
 if(client>=states.size()||!ptr)return;
 std::lock_guard lock(stats_mutex);auto&s=states[client];if(s.reports>=180)return;
 const auto now=Clock::now();if(s.start.time_since_epoch().count()==0)s.start=s.last=now;
 s.max_gap=std::max(s.max_gap,std::chrono::duration<double,std::milli>(now-s.last).count());s.last=now;
 auto*pcm=reinterpret_cast<rex::be<float>*>(base+ptr);std::uint64_t hash=1469598103934665603ull;
 for(unsigned ch=0;ch<6;ch++)for(unsigned n=0;n<256;n++){
  const float v=pcm[ch*256+n];const auto bits=std::bit_cast<std::uint32_t>(v);hash=(hash^bits)*1099511628211ull;
  if(!std::isfinite(v)){++s.bad;continue;}
  s.peak[ch]=std::max(s.peak[ch],std::abs(v));s.power[ch]+=double(v)*v;if(std::abs(v)>1)++s.clipped;
 }
 if(s.frames&&hash==s.hash)++s.repeated;s.hash=hash;++s.frames;
 const double seconds=std::chrono::duration<double>(now-s.start).count();
 if(seconds>=1){
  REXLOG_INFO("AUDIO frames={} seconds={:.3f} rate={:.1f} max_gap_ms={:.1f} invalid={} over_range={} repeats={} peaks={:.3f}/{:.3f}/{:.3f}/{:.3f}/{:.3f}/{:.3f} rms={:.3f}/{:.3f}",s.frames,seconds,s.frames/seconds,s.max_gap,s.bad,s.clipped,s.repeated,s.peak[0],s.peak[1],s.peak[2],s.peak[3],s.peak[4],s.peak[5],std::sqrt(s.power[0]/(s.frames*256)),std::sqrt(s.power[1]/(s.frames*256)));
  const auto reports=s.reports+1;s=AudioStats{};s.start=s.last=now;s.reports=reports;
 }
}
}
REX_HOOK_RAW(__imp__XAudioSubmitRenderDriverFrame){
 static auto*original=reinterpret_cast<PPCFunc*>(GetProcAddress(GetModuleHandleW(L"rexruntime.dll"),"__imp__XAudioSubmitRenderDriverFrame"));
 if(!original)REX_FATAL("Missing original XAudioSubmitRenderDriverFrame");
 if(REXCVAR_GET(trace_audio_frames))Inspect(base,ctx.r4.u32,ctx.r3.u32&0xFFFF);
 original(ctx,base);

}
