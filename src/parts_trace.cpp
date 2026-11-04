// Bounded diagnostics for the original song-part eligibility check.
#include <atomic>
#include <string>
#include <rex/hook.h>
#include <rex/cvar.h>
REXCVAR_DECLARE(bool, trace_song_catalog);
extern "C" REX_FUNC(__imp__sub_8226C018);
extern "C" REX_FUNC(__imp__sub_82260870);
extern "C" REX_FUNC(__imp__sub_8226BB50);
extern "C" REX_FUNC(__imp__sub_8225B3B0);
namespace {
thread_local unsigned part_depth=0;
std::atomic<unsigned> part_records{0};
std::uint32_t Word(std::uint8_t*b,std::uint32_t a){return a?std::uint32_t(*reinterpret_cast<rex::be<std::uint32_t>*>(b+a)):0;}
std::string Text(std::uint8_t*b,std::uint32_t a){if(!a)return "(null)";auto*p=reinterpret_cast<char*>(b+a);return std::string(p,strnlen(p,200));}
}
REX_HOOK_RAW(sub_8226C018){
 const bool trace=REXCVAR_GET(trace_song_catalog)&&part_records.fetch_add(1)<60;
 if(trace){REXLOG_INFO("PARTS song='{}' mode={} performer={:08X} users={:08X}",Text(base,ctx.r4.u32),ctx.r5.u32,ctx.r6.u32,ctx.r7.u32);++part_depth;}
 __imp__sub_8226C018(ctx,base);
 if(trace){--part_depth;REXLOG_INFO("PARTS missing_parts={}",ctx.r3.u32);}
}
REX_HOOK_RAW(sub_82260870){
 const auto user=ctx.r4.u32,out=ctx.r3.u32;
 __imp__sub_82260870(ctx,base);
 if(part_depth)REXLOG_INFO("PARTS user={:08X} instrument='{}' vtable={:08X} fields[48..84]={:08X},{:08X},{:08X},{:08X},{:08X},{:08X},{:08X},{:08X},{:08X},{:08X}",user,Text(base,Word(base,out)),Word(base,user),Word(base,user+48),Word(base,user+52),Word(base,user+56),Word(base,user+60),Word(base,user+64),Word(base,user+68),Word(base,user+72),Word(base,user+76),Word(base,user+80),Word(base,user+84));
}
REX_HOOK_RAW(sub_8226BB50){
 std::string song,instrument;
 if(part_depth){song=Text(base,ctx.r4.u32);instrument=Text(base,ctx.r5.u32);}
 __imp__sub_8226BB50(ctx,base);
 if(part_depth)REXLOG_INFO("PARTS rank song='{}' instrument='{}' value={}",song,instrument,ctx.f1.f64);
}
REX_HOOK_RAW(sub_8225B3B0){
 const auto out=ctx.r4.u32;
 __imp__sub_8225B3B0(ctx,base);
 if(part_depth){const auto begin=Word(base,out),end=Word(base,out+4);REXLOG_INFO("PARTS participating range={:08X}..{:08X} count={}",begin,end,(end-begin)/4);}
}

