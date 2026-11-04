// The SDK's XNetRandom fills every request with 0xBB. TBRB uses it to
// generate player GUIDs; identical GUIDs make every unused slot participate.
#include <windows.h>
#include <bcrypt.h>
#include <atomic>
#include <rex/hook.h>
REX_HOOK_RAW(__imp__NetDll_XNetRandom){
 const auto buffer=ctx.r4.u32,length=ctx.r5.u32;
 if(!length){ctx.r3.u64=0;return;}
 if(!buffer){ctx.r3.u64=10022;return;}
 const auto status=BCryptGenRandom(nullptr,base+buffer,length,BCRYPT_USE_SYSTEM_PREFERRED_RNG);
 ctx.r3.u64=BCRYPT_SUCCESS(status)?0:10055;
 static std::atomic<bool> reported{false};
 if(BCRYPT_SUCCESS(status)&&!reported.exchange(true))REXLOG_INFO("XNetRandom: using Windows random generator for distinct player identities");
}
