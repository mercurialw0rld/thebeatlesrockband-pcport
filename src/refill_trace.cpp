#include "thread_priority.h"
#include <chrono>
#include <windows.h>
#include <rex/hook.h>
#include <rex/cvar.h>
REXCVAR_DECLARE(bool, trace_audio_frames);
extern "C" {REX_FUNC(__imp__sub_824D7B10);REX_FUNC(__imp__sub_824D2060);REX_FUNC(__imp__sub_824AFBC8);}
namespace {
using Clock=std::chrono::steady_clock;
struct RefillStats {bool active=false;Clock::time_point start=Clock::now();unsigned polls=0,cursors=0,flips=0,fills=0,requested=0,returned=0,half=2;double poll_ms=0,max_ms=0,fill_ms=0;unsigned reports=0;};
thread_local RefillStats stats;
}
REX_HOOK_RAW(sub_824D7B10){
 beatles::PrioritizeAudioThread();
 if(!REXCVAR_GET(trace_audio_frames)){__imp__sub_824D7B10(ctx,base);return;}
 const auto before=Clock::now();stats.active=true;__imp__sub_824D7B10(ctx,base);stats.active=false;
 const auto now=Clock::now();const double duration=std::chrono::duration<double,std::milli>(now-before).count();stats.poll_ms+=duration;stats.max_ms=std::max(stats.max_ms,duration);++stats.polls;
 const double elapsed=std::chrono::duration<double>(now-stats.start).count();
 if(elapsed>=1 && stats.reports<180){
  REXLOG_INFO("REFILL thread={} polls_hz={:.1f} cursor_reads={} half_flips={} fills={} requested={} returned={} poll_avg_ms={:.3f} poll_max_ms={:.1f} fill_total_ms={:.1f}",GetCurrentThreadId(),stats.polls/elapsed,stats.cursors,stats.flips,stats.fills,stats.requested,stats.returned,stats.poll_ms/stats.polls,stats.max_ms,stats.fill_ms);
  const auto half=stats.half,reports=stats.reports+1;stats=RefillStats{};stats.start=now;stats.half=half;stats.reports=reports;
 }
}
REX_HOOK_RAW(sub_824D2060){
 __imp__sub_824D2060(ctx,base);
 if(stats.active){++stats.cursors;const auto half=ctx.r3.u32>>10;if(half!=stats.half){++stats.flips;stats.half=half;}}
}
REX_HOOK_RAW(sub_824AFBC8){
 if(!stats.active){__imp__sub_824AFBC8(ctx,base);return;}
 const auto count_ptr=ctx.r5.u32;
 const unsigned requested=(stats.active&&count_ptr)?static_cast<std::uint32_t>(*reinterpret_cast<rex::be<std::uint32_t>*>(base+count_ptr)):0;
 const auto before=Clock::now();__imp__sub_824AFBC8(ctx,base);
 if(stats.active){++stats.fills;stats.requested+=requested;stats.returned+=count_ptr?static_cast<std::uint32_t>(*reinterpret_cast<rex::be<std::uint32_t>*>(base+count_ptr)):0;stats.fill_ms+=std::chrono::duration<double,std::milli>(Clock::now()-before).count();}
}
