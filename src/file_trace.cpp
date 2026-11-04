#include <windows.h>
#include <algorithm>
#include <atomic>
#include <mutex>
#include <string>
#include <unordered_map>
#include <rex/assert.h>
#include <rex/cvar.h>
#include <rex/hook.h>

REXCVAR_DEFINE_BOOL(trace_game_files, false, "Debug", "Trace bounded game file I/O diagnostics");
namespace {
std::mutex file_mutex;
std::unordered_map<std::uint32_t,std::string> file_names;
std::atomic<unsigned> records{0};
PPCFunc* Original(const char* name) {
  auto* p = reinterpret_cast<PPCFunc*>(GetProcAddress(GetModuleHandleW(L"rexruntime.dll"),name));
  if (!p) REX_FATAL("Missing original file API {}",name);
  return p;
}
std::uint32_t Load32(std::uint8_t* base,std::uint32_t addr) {
  return *reinterpret_cast<rex::be<std::uint32_t>*>(base+addr);
}
}
REX_HOOK_RAW(__imp__NtCreateFile) {
  static auto* original = Original("__imp__NtCreateFile");
  if (!REXCVAR_GET(trace_game_files)) { original(ctx,base); return; }
  const auto handle_out = ctx.r3.u32;
  const auto attrs = ctx.r5.u32;
  std::string name;
  if (attrs) {
    const auto ansi = Load32(base,attrs+4);
    if (ansi) {
      const auto length = std::uint16_t(*reinterpret_cast<rex::be<std::uint16_t>*>(base+ansi));
      const auto data = Load32(base,ansi+4);
      if (data) name.assign(reinterpret_cast<char*>(base+data),std::min<unsigned>(length,1024));
    }
  }
  original(ctx,base);
  const auto handle = handle_out ? Load32(base,handle_out) : 0;
  if (ctx.r3.u32 == 0) { std::lock_guard lock(file_mutex); file_names[handle]=name; }
  if (records.fetch_add(1)<3000) REXLOG_INFO("FILE open '{}' -> status={:08X} handle={:08X}",name,ctx.r3.u32,handle);
}
REX_HOOK_RAW(__imp__NtReadFile) {
  static auto* original = Original("__imp__NtReadFile");
  if (!REXCVAR_GET(trace_game_files)) { original(ctx,base); return; }
  const auto handle=ctx.r3.u32, iosb=ctx.r7.u32, length=ctx.r9.u32, offset_ptr=ctx.r10.u32;
  const std::uint64_t offset = offset_ptr ? std::uint64_t(*reinterpret_cast<rex::be<std::uint64_t>*>(base+offset_ptr)) : ~std::uint64_t(0);
  original(ctx,base);
  if (records.fetch_add(1)<3000) {
    std::string name;
    { std::lock_guard lock(file_mutex); auto it=file_names.find(handle); if(it!=file_names.end())name=it->second; }
    REXLOG_INFO("FILE read '{}' handle={:08X} offset={} length={} -> status={:08X} iosb={:08X} bytes={}",name,handle,offset,length,ctx.r3.u32,iosb?Load32(base,iosb):0,iosb?Load32(base,iosb+4):0);
  }
}
