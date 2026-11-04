#include "thread_priority.h"
#include <windows.h>
#include <array>
#include <chrono>
#include <algorithm>
#include <rex/cvar.h>
#include <rex/logging.h>
#include <rex/hook.h>
REXCVAR_DECLARE(bool, trace_audio_frames);
#include "precise_sleep.h"
#include "audio_pacing.h"
REXCVAR_DEFINE_BOOL(audio_render_pacing, true, "Audio", "Space guest render quanta so the music producer observes each buffer transition");
REXCVAR_DECLARE(std::int32_t, audio_maxqframes);
void PaceAudioRenderFrame(unsigned client){
 if(!REXCVAR_GET(audio_render_pacing)||client>=8)return;
 using Clock=std::chrono::steady_clock;
 struct Pacer {beatles::ShortTimer timer;unsigned initial=0;std::uint64_t frames=0;Clock::time_point epoch{},last{};bool warned=false;unsigned measured=0;double waited_ms=0;Clock::time_point report{};};
 thread_local std::array<Pacer,8> pacers;
 auto& p=pacers[client];auto now=Clock::now();
 const unsigned prefill=std::clamp(REXCVAR_GET(audio_maxqframes),4,64);
 if(p.initial<prefill){++p.initial;p.epoch=p.last=now;return;}
 // Fixed 256-sample / 48-kHz Xbox render quanta. Keep the initial queue for
 // device jitter, but do not render two new quanta back-to-back when a Windows
 // audio processing pass releases several semaphore tokens at once. TBRB's
 // tiny looping music buffer is refilled by a separate polling thread.
 auto due=p.epoch+std::chrono::nanoseconds((++p.frames*256*1000000000ull)/48000);
 due=std::max(due,p.last+std::chrono::milliseconds(3));
 if(now-due>std::chrono::milliseconds(80)){p.epoch=now;p.frames=0;due=now;}
 const auto ns=std::chrono::duration_cast<std::chrono::nanoseconds>(due-now).count();
 if(ns>0 && !p.timer.Wait(-((ns+99)/100)) && !p.warned){REXLOG_WARN("Audio render pacing: high-resolution timer unavailable");p.warned=true;}
 p.last=Clock::now();
 if(REXCVAR_GET(trace_audio_frames)){if(p.report.time_since_epoch().count()==0)p.report=p.last;p.waited_ms+=std::chrono::duration<double,std::milli>(p.last-now).count();++p.measured;const double elapsed=std::chrono::duration<double>(p.last-p.report).count();if(elapsed>=1){REXLOG_INFO("RENDER-PACE calls_hz={:.1f} wait_avg_ms={:.3f} total_frames={} client={}",p.measured/elapsed,p.waited_ms/p.measured,p.frames,client);p.measured=0;p.waited_ms=0;p.report=p.last;}}
 if(p.frames==1)REXLOG_INFO("Audio render pacing: 256 samples at 48000 Hz, one quantum every 5.333 ms after queue prefill");
}

extern "C" REX_FUNC(__imp__sub_82648278);
REX_HOOK_RAW(sub_82648278){beatles::PrioritizeAudioThread();PaceAudioRenderFrame(0);__imp__sub_82648278(ctx,base);}
