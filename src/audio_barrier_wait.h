#pragma once
#include <windows.h>
#include <atomic>
#include <bit>
#include <condition_variable>
#include <cstdint>
#include <map>
#include <mutex>
namespace beatles {
// Xenon's byte flags have no generation counter. A Windows worker can reenter
// the same gate before its peer observes the zero, losing an arrival (ABA).
// Track completion generations on the host, retaining the guest participant
// mask and memory barrier. Never release a phase before all workers arrive.
inline unsigned WaitAudioBarrier(std::uint8_t* base,std::uint32_t object,std::uint32_t gate,std::uint8_t participant,void(*stalled)(std::uint32_t,std::uint32_t,unsigned,std::uint64_t,std::uint64_t)=nullptr){
 auto word=[&](std::uint32_t address){return std::byteswap(*reinterpret_cast<volatile std::uint32_t*>(base+address));};
 if(!word(object+304))return 0;
 struct Phase {std::uint64_t generation=0;unsigned arrived=0,expected=0;std::condition_variable changed;};
 static std::mutex mutex;
 static std::map<std::uintptr_t,Phase> phases;
 std::unique_lock lock(mutex);
 auto& phase=phases.try_emplace(reinterpret_cast<std::uintptr_t>(base+gate)).first->second;
 if(!phase.arrived){
  phase.expected=0;
  for(unsigned i=0;i<6;i++)if(word(object+308+i*4))phase.expected|=1u<<i;
 }
 const auto generation=phase.generation;
 phase.arrived|=1u<<participant;
 *reinterpret_cast<volatile std::uint8_t*>(base+gate+participant)=1;
 if(phase.arrived==phase.expected){
  *reinterpret_cast<volatile std::uint64_t*>(base+gate)=0;
  phase.arrived=0;
  ++phase.generation;
  lock.unlock();
  phase.changed.notify_all();
  return 0;
 }
 unsigned waits=0;
 while(phase.generation==generation){
  ++waits;
  if(phase.changed.wait_for(lock,std::chrono::milliseconds(500))==std::cv_status::timeout && stalled && waits==1){
   const auto observed=*reinterpret_cast<volatile std::uint64_t*>(base+gate);
   std::uint64_t expected=0;
   for(unsigned i=0;i<6;i++)if(phase.expected&(1u<<i))expected|=std::uint64_t(1)<<(8*i);
   stalled(object,gate,participant,observed,expected);
  }
 }
 return waits;
}
}
