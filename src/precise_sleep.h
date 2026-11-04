#pragma once
#include <windows.h>
#include <cstdint>
namespace beatles {
class ShortTimer {
 HANDLE timer_=CreateWaitableTimerExW(nullptr,nullptr,CREATE_WAITABLE_TIMER_HIGH_RESOLUTION,TIMER_MODIFY_STATE|SYNCHRONIZE);
 public:
 ShortTimer()=default;
 ShortTimer(const ShortTimer&)=delete;
 ShortTimer& operator=(const ShortTimer&)=delete;
 ~ShortTimer(){if(timer_)CloseHandle(timer_);}
 bool Wait(std::int64_t relative_ticks){
  if(!timer_||relative_ticks>=0)return false;
  LARGE_INTEGER due{};due.QuadPart=relative_ticks;
  return SetWaitableTimerEx(timer_,&due,0,nullptr,nullptr,nullptr,0) && WaitForSingleObjectEx(timer_,INFINITE,FALSE)==WAIT_OBJECT_0;
 }
};
}
