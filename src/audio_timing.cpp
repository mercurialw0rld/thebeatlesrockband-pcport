#include <atomic>
#include <rex/hook.h>
#include "audio_barrier_wait.h"
REX_HOOK_RAW(sub_82646F68){
 const auto waits=beatles::WaitAudioBarrier(base,ctx.r3.u32,ctx.r4.u32,base[ctx.r13.u32+268],[](std::uint32_t object,std::uint32_t gate,unsigned participant,std::uint64_t observed,std::uint64_t expected){REXLOG_WARN("Audio barrier stalled object={:08X} gate={:08X} participant={} observed={:016X} expected={:016X}",object,gate,participant,observed,expected);});
 static std::atomic<bool> reported{false};
 if(waits&&!reported.exchange(true))REXLOG_INFO("Audio worker barrier: generation-tracked wait/wake active");
}
