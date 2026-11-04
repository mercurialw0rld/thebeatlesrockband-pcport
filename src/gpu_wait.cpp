// Xenon's cctpl/db16cyc wait hints disappear in the recompiled code. Keep the
// original fence/timeout handling, but stop burning a core when no work completes.
#include <windows.h>
#include <cstdint>
#include <rex/cvar.h>
#include <rex/hook.h>
#include "precise_sleep.h"

REXCVAR_DEFINE_BOOL(gpu_wait_yield, true, "GPU", "Yield during repeated guest GPU fence polls without skipping fence completion");

extern "C" REX_FUNC(__imp__sub_826665D8);
REX_HOOK_RAW(sub_826665D8) {
  const auto wait_context = ctx.r3.u32;
  __imp__sub_826665D8(ctx, base);
  if (!REXCVAR_GET(gpu_wait_yield)) return;

  struct PollState {
    beatles::ShortTimer timer;
    std::uint32_t context=0, device=0, counter=0, unchanged=0;
  };
  thread_local PollState poll;
  if (!ctx.r3.u32) { poll.unchanged=0; return; }

  const auto load=[base](std::uint32_t address) {
    return std::uint32_t(*reinterpret_cast<rex::be<std::uint32_t>*>(base+address));
  };
  const auto device=load(wait_context);
  const auto counter_address=load(device+10896);
  const auto counter=load(counter_address);
  if (poll.context!=wait_context || poll.device!=device || poll.counter!=counter) {
    poll.context=wait_context; poll.device=device; poll.counter=counter;
    poll.unchanged=0;
    return;
  }
  // Short waits still spin. Longer waits release the CPU in 100-us slices,
  // then return to the original caller to recheck the actual fence value.
  if (++poll.unchanged>=32) {
    if (!poll.timer.Wait(-1000)) SwitchToThread();
  } else {
    YieldProcessor();
  }
}
