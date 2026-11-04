#include <windows.h>
#include <atomic>
#include <string>
#include <rex/hook.h>
#include <rex/cvar.h>
REXCVAR_DEFINE_BOOL(trace_song_catalog, false, "Debug", "Trace song catalogue and loader callbacks");
namespace {
std::atomic<unsigned> catalog_records{0},debug_records{0};
std::uint32_t U32(std::uint8_t* b,std::uint32_t a){return a?std::uint32_t(*reinterpret_cast<rex::be<std::uint32_t>*>(b+a)):0;}
int Count(std::uint8_t*b,std::uint32_t a){return a?int(*reinterpret_cast<rex::be<std::int16_t>*>(b+a+8)):-1;}
std::string Str(std::uint8_t*b,std::uint32_t a){if(!a)return "(null)";auto*p=reinterpret_cast<char*>(b+a);return std::string(p,strnlen(p,256));}
void Report(const char*fn,PPCContext&c,std::uint8_t*b,bool after,std::uint32_t self,std::uint32_t data){
 if(!REXCVAR_GET(trace_song_catalog)||catalog_records.fetch_add(1)>500)return;
 auto all=U32(b,self+44);
 REXLOG_INFO("CATALOG {} {} this={:08X} data={:08X} nodes={} catalogue={:08X} songs={} r3={:08X} r4={:08X} r5={:08X} r6={:08X} lr={:08X}",fn,after?"exit":"enter",self,data,Count(b,data),all,Count(b,all),c.r3.u32,c.r4.u32,c.r5.u32,c.r6.u32,std::uint32_t(c.lr));
}
}
#define TRACE_SONG(name,adjust,has_data) \
 extern "C" REX_FUNC(__imp__##name); \
 REX_HOOK_RAW(name){const auto self=ctx.r3.u32+(adjust),data=(has_data)?ctx.r4.u32:0;Report(#name,ctx,base,false,self,data);__imp__##name(ctx,base);Report(#name,ctx,base,true,self,data);}
TRACE_SONG(sub_82257BB0,0,false)
TRACE_SONG(sub_8226A838,-40,false)
TRACE_SONG(sub_82258658,-40,false)
TRACE_SONG(sub_8226E218,-40,false)
TRACE_SONG(sub_822581D8,0,true)
TRACE_SONG(sub_8226E008,0,true)
TRACE_SONG(sub_8226D700,0,true)
REX_HOOK_RAW(__imp__OutputDebugStringA){
 static auto original=reinterpret_cast<PPCFunc*>(GetProcAddress(GetModuleHandleW(L"rexruntime.dll"),"__imp__OutputDebugStringA"));
 if(REXCVAR_GET(trace_song_catalog)&&ctx.r3.u32&&debug_records.fetch_add(1)<2000)REXLOG_INFO("GAME {}",Str(base,ctx.r3.u32));
 if(original)original(ctx,base);
}
