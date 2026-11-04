#define BEATLES_INPUT_EXPORTS
#include "controller_bridge.h"
#include <SDL3/SDL.h>
#include <algorithm>
#include <chrono>
#include <cstdio>
#include <cstring>
#include <mutex>
#include <string>
#include <thread>
#include <vector>

namespace {
struct Device {SDL_JoystickID instance;SDL_Joystick* joystick;BR_Device info{};BR_State state{};};
struct Bridge {
 std::mutex mutex;std::vector<Device> devices;int ready=0;
 Bridge(){std::thread([this]{Run();}).detach();}
 void Run(){
  SDL_SetHint(SDL_HINT_JOYSTICK_ALLOW_BACKGROUND_EVENTS,"1");
  if(!SDL_InitSubSystem(SDL_INIT_JOYSTICK)){std::lock_guard lock(mutex);ready=-1;return;}
  auto scan=std::chrono::steady_clock::time_point{};
  for(;;){
   SDL_PumpEvents();SDL_UpdateJoysticks();SDL_FlushEvents(SDL_EVENT_FIRST,SDL_EVENT_LAST);
   bool idle=false;
   {
    std::lock_guard lock(mutex);
    auto now=std::chrono::steady_clock::now();
    if(now>=scan){
     scan=now+std::chrono::milliseconds(500);
     for(auto it=devices.begin();it!=devices.end();){
      if(!SDL_JoystickConnected(it->joystick)){SDL_CloseJoystick(it->joystick);it=devices.erase(it);}else ++it;
     }
     int count=0;auto* ids=SDL_GetJoysticks(&count);
     for(int i=0;i<count;i++){
      if(std::any_of(devices.begin(),devices.end(),[&](const Device&d){return d.instance==ids[i];}))continue;
      Device d{};d.instance=ids[i];d.joystick=SDL_OpenJoystick(ids[i]);if(!d.joystick)continue;
      char guid[33];SDL_GUIDToString(SDL_GetJoystickGUID(d.joystick),guid,sizeof(guid));
      const char* serial=SDL_GetJoystickSerial(d.joystick);const char* path=SDL_GetJoystickPath(d.joystick);
      std::string identity=serial&&*serial?serial:path?path:"";
      std::uint64_t hash=14695981039346656037ull;for(unsigned char c:identity){hash^=c;hash*=1099511628211ull;}
      std::snprintf(d.info.id,sizeof(d.info.id),"%s-%016llx",guid,static_cast<unsigned long long>(hash));
      // Identical devices without serial/path are distinguished by attachment order.
      const std::string root_id=d.info.id;int duplicate=0;
      while(std::any_of(devices.begin(),devices.end(),[&](const Device&existing){return std::strcmp(existing.info.id,d.info.id)==0;}))
       std::snprintf(d.info.id,sizeof(d.info.id),"%s-%d",root_id.c_str(),++duplicate);
      const char* name=SDL_GetJoystickName(d.joystick);std::snprintf(d.info.name,sizeof(d.info.name),"%s",name?name:"Controller");
      d.info.buttons=std::clamp(SDL_GetNumJoystickButtons(d.joystick),0,64);
      d.info.axes=std::clamp(SDL_GetNumJoystickAxes(d.joystick),0,16);
      d.info.hats=std::clamp(SDL_GetNumJoystickHats(d.joystick),0,4);
      devices.push_back(d);
     }
     SDL_free(ids);ready=1;
    }
    for(auto&d:devices){
     d.state={};d.state.axis_count=d.info.axes;d.state.hat_count=d.info.hats;
     for(int b=0;b<d.info.buttons;b++)if(SDL_GetJoystickButton(d.joystick,b))d.state.buttons|=1ull<<b;
     for(int a=0;a<d.info.axes;a++)d.state.axes[a]=SDL_GetJoystickAxis(d.joystick,a);
     for(int h=0;h<d.info.hats;h++)d.state.hats[h]=SDL_GetJoystickHat(d.joystick,h);
    }
    idle=devices.empty();
   }
   std::this_thread::sleep_for(std::chrono::milliseconds(idle?50:4));
  }
 }
};
// The DLL stays loaded for the process lifetime. The worker and SDL objects are
// owned by this process-lifetime service, never by an editor window or guest call.
Bridge& Service(){static auto* service=new Bridge;return *service;}
}
BR_API int BR_List(BR_Device* out,int capacity){auto&s=Service();std::lock_guard lock(s.mutex);if(s.ready<=0)return s.ready==0?-2:-1;int count=int(s.devices.size());if(out)for(int i=0;i<std::min(count,capacity);i++)out[i]=s.devices[i].info;return count;}
BR_API int BR_Read(const char* id,BR_State* out){if(!id||!out)return 0;auto&s=Service();std::lock_guard lock(s.mutex);for(auto&d:s.devices)if(std::strcmp(id,d.info.id)==0){*out=d.state;return 1;}return 0;}
BR_API float BR_Value(const BR_State* state,const char* token){
 if(!state||!token)return 0;int index=0,rest=0,full=0,mask=0;
 if(std::sscanf(token,"button:%d",&index)==1){return index>=0&&index<64&&((state->buttons>>index)&1)?1.f:0.f;}
 if(std::sscanf(token,"hat:%d:%d",&index,&mask)==2){return index>=0&&index<state->hat_count&&(state->hats[index]&mask)?1.f:0.f;}
 if(std::sscanf(token,"axis:%d:%d:%d",&index,&rest,&full)==3&&index>=0&&index<state->axis_count&&rest!=full){return std::clamp(float(state->axes[index]-rest)/float(full-rest),0.f,1.f);}
 return 0;
}
