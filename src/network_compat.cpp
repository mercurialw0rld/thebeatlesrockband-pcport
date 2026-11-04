#include "thread_priority.h"
// The SDK stub leaves r3 equal to the caller ID, which falsely reports a
// completed network request. Report the unavailable network explicitly.
#include <atomic>
#include <chrono>
#include <rex/hook.h>
#include <rex/system/xthread.h>
#include <rex/cvar.h>
REXCVAR_DECLARE(bool, trace_frame_times);
#include "precise_sleep.h"
REXCVAR_DEFINE_BOOL(offline_receive_backoff, true, "Network", "Back off rapid failed receives in the offline Quazal worker");

namespace {
using RetryClock=std::chrono::steady_clock;
struct OfflineRetryState {
  beatles::ShortTimer timer;
  unsigned count=0;
  RetryClock::time_point burst{};
  bool active=false,pending=false,reported=false;
};
thread_local OfflineRetryState offline_retry;
}

REX_HOOK_RAW(__imp__NetDll_WSARecvFrom) {
  // The SDK has no receive implementation and returns SOCKET_ERROR without
  // setting last-error. Keep the failure explicit, including zero bytes read.
  if (ctx.r7.u32) *reinterpret_cast<rex::be<std::uint32_t>*>(base+ctx.r7.u32)=0;
  rex::system::XThread::SetLastError(10050); // WSAENETDOWN
  ctx.r3.u64=0xFFFFFFFFu;

  if (!REXCVAR_GET(offline_receive_backoff)) return;
  auto& retry=offline_retry;
  if (!retry.active) return;
  if (!retry.count) retry.burst=RetryClock::now();
  if (++retry.count<16) return;
  retry.count=0;
  if (RetryClock::now()-retry.burst<std::chrono::milliseconds(1)) retry.pending=true;
}

extern "C" REX_FUNC(__imp__sub_8293B5A8);
REX_HOOK_RAW(sub_8293B5A8) {
  // Only the background worker's dispatch loop gets a backoff. Its original
  // function releases the network mutex through 828A4728 before returning.
  // Sleeping inside WSARecvFrom would retain that mutex and block other users.
  if (!REXCVAR_GET(offline_receive_backoff) || std::uint32_t(ctx.lr)!=0x8293B9D8) {
    __imp__sub_8293B5A8(ctx,base); return;
  }
  auto& retry=offline_retry;
  retry.active=true; retry.pending=false;
  __imp__sub_8293B5A8(ctx,base);
  retry.active=false;
  if (!retry.pending) return;
  if (!retry.reported) {
    REXLOG_INFO("Offline receive worker: rapid failed retries yield CPU after releasing network mutex (1 ms per 16 hot retries)");
    retry.reported=true;
  }
  if (!retry.timer.Wait(-10000)) SwitchToThread();
}

REX_HOOK_RAW(__imp__NetDll_WSAGetOverlappedResult) {
  beatles::DeprioritizeOfflineNetworkThread();
  if(REXCVAR_GET(trace_frame_times)){
    static thread_local bool described=false;
    if(!described){REXLOG_INFO("Network poll thread={} return={:08X} wait={} socket={:08X}",GetCurrentThreadId(),std::uint32_t(ctx.lr),ctx.r7.u32,ctx.r4.u32);described=true;}
  }
  if (ctx.r6.u32) *reinterpret_cast<rex::be<std::uint32_t>*>(base + ctx.r6.u32) = 0;
  rex::system::XThread::SetLastError(10050);  // WSAENETDOWN.
  ctx.r3.u64 = 0;                          // FALSE, not a completed packet.
  static std::atomic<bool> reported{false};
  if (!reported.exchange(true)) REXLOG_INFO("Network overlapped request unavailable (offline)");
}
#include <windows.h>
#include <rex/ppc/stack.h>
REX_HOOK_RAW(__imp__NetDll_WSAWaitForMultipleEvents){
 const auto count=ctx.r3.u32,handles=ctx.r4.u32,wait_all=ctx.r5.u32,timeout_ms=ctx.r6.u32,alertable=ctx.r7.u32;
 if(!count||count>64||!handles){rex::system::XThread::SetLastError(87);ctx.r3.u64=0xFFFFFFFF;return;}
 static auto*wait=reinterpret_cast<PPCFunc*>(GetProcAddress(GetModuleHandleW(L"rexruntime.dll"),"__imp__NtWaitForMultipleObjectsEx"));
 if(!wait)REX_FATAL("Missing original NtWaitForMultipleObjectsEx");
 rex::ppc::stack_guard stack(ctx);
 std::uint32_t timeout_address=0;
 if(timeout_ms!=0xFFFFFFFF){
  const std::int64_t interval=-std::int64_t(timeout_ms)*10000;
  timeout_address=rex::ppc::stack_push(ctx,base,interval);
 }
 // WSA takes milliseconds and a BOOL; NtWait takes signed 100-ns ticks and
 // WaitAll=0 / WaitAny=1. Preserve timeout and signalled-event index results.
 do{
  ctx.r3.u64=count;ctx.r4.u64=handles;ctx.r5.u64=wait_all?0:1;
  ctx.r6.u64=1;ctx.r7.u64=alertable;ctx.r8.u64=timeout_address;
  wait(ctx,base);
 }while(ctx.r3.u32==0x101);
 if(ctx.r3.u32&0x80000000){rex::system::XThread::SetLastError(87);ctx.r3.u64=0xFFFFFFFF;}
 static std::atomic<bool> reported{false};
 if(!reported.exchange(true))REXLOG_INFO("Network event wait: corrected millisecond timeout and wait mode");
}

