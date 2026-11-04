#include <windows.h>
#include <chrono>
#include <rex/hook.h>
#include <rex/cvar.h>
#include <rex/chrono/clock.h>
#include <rex/system/xthread.h>
#include "precise_sleep.h"
REXCVAR_DEFINE_BOOL(precise_guest_sleep, false, "CPU", "Preserve short guest waits with a high-resolution Windows timer");
REXCVAR_DECLARE(bool, trace_audio_frames);
REX_HOOK_RAW(__imp__KeDelayExecutionThread){
 static auto* original=reinterpret_cast<PPCFunc*>(GetProcAddress(GetModuleHandleW(L"rexruntime.dll"),"__imp__KeDelayExecutionThread"));
 if(!original)REX_FATAL("Missing original KeDelayExecutionThread");
 if(!REXCVAR_GET(precise_guest_sleep)||ctx.r4.u32||!ctx.r5.u32){original(ctx,base);return;}
 const std::int64_t requested=*reinterpret_cast<rex::be<std::int64_t>*>(base+ctx.r5.u32);
 // Preserve alertable, absolute, zero/yield and longer waits in the runtime.
 if(requested>=0||requested < -100000){original(ctx,base);return;}
 struct Stats {unsigned old_count=0,new_count=0;double old_ms=0,new_ms=0;};
 thread_local Stats stats;
 const bool trace=REXCVAR_GET(trace_audio_frames);
 using Clock=std::chrono::steady_clock;
 auto start=Clock::now();
 if(trace && requested==-10000 && stats.old_count<4){
  original(ctx,base);stats.old_ms+=std::chrono::duration<double,std::milli>(Clock::now()-start).count();++stats.old_count;return;
 }
 thread_local beatles::ShortTimer timer;
 rex::system::XThread::CheckTitleTermination();
 const auto scaled=rex::chrono::Clock::ScaleGuestDurationFileTime(requested);
 if(!timer.Wait(scaled)){original(ctx,base);return;}
 rex::system::XThread::CheckTitleTermination();
 ctx.r3.u64=0;
 if(trace && requested==-10000 && stats.new_count<16){
  stats.new_ms+=std::chrono::duration<double,std::milli>(Clock::now()-start).count();
  if(++stats.new_count==16)REXLOG_INFO("SHORT-SLEEP thread={} requested_ms=1 original_avg_ms={:.3f} precise_avg_ms={:.3f}",GetCurrentThreadId(),stats.old_count?stats.old_ms/stats.old_count:0,stats.new_ms/stats.new_count);
 }
}
