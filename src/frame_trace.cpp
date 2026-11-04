// Optional, memory-only guest-frame telemetry. An external reader persists
// samples, so the rendering thread performs no logging, allocation or file I/O
// after the first swap. This measures VdSwap submissions, not monitor scanout.
#include <windows.h>
#include <atomic>
#include <cstdint>
#include <mutex>
#include <new>
#include <rex/cvar.h>
#include <rex/hook.h>
REXCVAR_DEFINE_BOOL(trace_frame_times, false, "Diagnostics", "Publish guest frame timings in a local shared-memory ring");
namespace {
constexpr unsigned kCapacity=8192;
struct Sample {std::int64_t begin,end;};
struct SharedFrames {
 std::uint32_t magic=0x42524654,version=1;
 std::int64_t frequency=0;
 std::atomic<std::uint32_t> committed{0};
 std::uint32_t capacity=kCapacity,producer=0,reserved=0;
 Sample samples[kCapacity]{};
};
static_assert(offsetof(SharedFrames,samples)==32);
class FrameTrace {
 HANDLE mapping_=nullptr;SharedFrames* data_=nullptr;std::mutex mutex_;
 public:
 FrameTrace(){
  wchar_t name[96];swprintf_s(name,L"Local\\BeatlesPort.FrameTimes.%lu",GetCurrentProcessId());
  mapping_=CreateFileMappingW(INVALID_HANDLE_VALUE,nullptr,PAGE_READWRITE,0,sizeof(SharedFrames),name);
  if(mapping_)data_=static_cast<SharedFrames*>(MapViewOfFile(mapping_,FILE_MAP_ALL_ACCESS,0,0,sizeof(SharedFrames)));
  if(data_){new(data_) SharedFrames;LARGE_INTEGER frequency;QueryPerformanceFrequency(&frequency);data_->frequency=frequency.QuadPart;data_->producer=GetCurrentThreadId();REXLOG_INFO("Frame telemetry enabled: Local\\BeatlesPort.FrameTimes.{}, guest VdSwap timings",GetCurrentProcessId());}
  else REXLOG_WARN("Frame telemetry could not create shared memory: {}",GetLastError());
 }
 ~FrameTrace(){if(data_)UnmapViewOfFile(data_);if(mapping_)CloseHandle(mapping_);}
 void Record(std::int64_t begin,std::int64_t end){if(!data_)return;std::lock_guard lock(mutex_);auto n=data_->committed.load(std::memory_order_relaxed);data_->samples[n%kCapacity]={begin,end};data_->committed.store(n+1,std::memory_order_release);}
};
}
REX_HOOK_RAW(__imp__VdSwap){
 static auto* original=reinterpret_cast<PPCFunc*>(GetProcAddress(GetModuleHandleW(L"rexruntime.dll"),"__imp__VdSwap"));
 if(!original)REX_FATAL("Missing original VdSwap");
 if(!REXCVAR_GET(trace_frame_times)){original(ctx,base);return;}
 static FrameTrace trace;LARGE_INTEGER begin,end;QueryPerformanceCounter(&begin);original(ctx,base);QueryPerformanceCounter(&end);trace.Record(begin.QuadPart,end.QuadPart);
}
