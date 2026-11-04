from pathlib import Path
import sys
source=Path(sys.argv[1]).read_text(encoding='utf-8-sig')
def method(name):
 token='void PipelineCache::'+name+'() {'
 if token not in source:return ''
 start=source.index(token);brace=source.index('{',start);depth=1;pos=brace+1
 while depth:
  depth+=(source[pos]=='{')-(source[pos]=='}');pos+=1
 return source[start:pos]+'\n'
# Compile the real method bodies with event/worker test doubles. This exercises
# the submission wait rather than duplicating its policy in test code.
await_body=method('AwaitPipelineCreation')
body=method('EndSubmission')+await_body
assert body
template=r'''
#include <atomic>
#include <chrono>
#include <condition_variable>
#include <future>
#include <memory>
#include <mutex>
#include <cstdio>
#include <thread>
#include <vector>
bool flag_d3d12_nonblocking_pipeline_creation=true,flag_async_shader_compilation=true;
#define REXCVAR_GET(name) flag_##name
namespace rex::thread {
struct Event {std::mutex m;std::condition_variable cv;bool ready=false;std::atomic<unsigned> waits{0};
 void Reset(){std::lock_guard lock(m);ready=false;}
 void Set(){std::lock_guard lock(m);ready=true;cv.notify_all();}
 void Wait(){std::unique_lock lock(m);++waits;cv.wait(lock,[&]{return ready;});}
};
void Wait(Event* e,bool){e->Wait();}
}
struct PipelineCache {
 std::atomic<bool> shader_storage_file_flush_needed_{false},pipeline_storage_file_flush_needed_{false};
 std::mutex storage_write_request_lock_,creation_request_lock_;
 std::condition_variable storage_write_request_cond_,creation_request_cond_;
 bool storage_write_flush_shaders_=false,storage_write_flush_pipelines_=false,creation_completion_set_event_=false;
 std::vector<int> creation_threads_{0};unsigned creation_threads_busy_=1;
 std::unique_ptr<rex::thread::Event> creation_completion_event_=std::make_unique<rex::thread::Event>();
 std::atomic<unsigned> drained{0};
 void CreateQueuedPipelinesOnProcessorThread(){++drained;}
 void EndSubmission();void AwaitPipelineCreation();
 void FinishWorker(){std::lock_guard lock(creation_request_lock_);creation_threads_busy_=0;creation_completion_event_->Set();}
};
/*PRODUCTION_METHODS*/
int main(){using namespace std::chrono_literals;int failures=0;
 auto check=[&](bool ok,const char* name){std::printf("%s %s\n",ok?"PASS":"FAIL",name);if(!ok)++failures;};
 {
  PipelineCache c;c.shader_storage_file_flush_needed_=true;c.pipeline_storage_file_flush_needed_=true;
  auto done=std::async(std::launch::async,[&]{c.EndSubmission();});
  bool returned=done.wait_for(1s)==std::future_status::ready;
  c.FinishWorker();done.get();
  check(returned,"async submission returns while an unrelated compiler worker is held");
  check(c.drained==0,"render thread does not steal background compilation jobs");
  check(c.storage_write_flush_shaders_&&c.storage_write_flush_pipelines_,"shader and pipeline persistence flush still requested");
 }
 for(int mode=0;mode<2;++mode){
  PipelineCache c;flag_d3d12_nonblocking_pipeline_creation=(mode!=0);flag_async_shader_compilation=(mode==0);
  auto done=std::async(std::launch::async,[&]{c.EndSubmission();});
  auto limit=std::chrono::steady_clock::now()+1s;
  while(!c.creation_completion_event_->waits&&std::chrono::steady_clock::now()<limit)std::this_thread::sleep_for(1ms);
  bool blocked=c.creation_completion_event_->waits&&done.wait_for(0ms)!=std::future_status::ready;
  c.FinishWorker();done.get();
  check(blocked,mode==0?"feature-off preserves legacy completion waits":"synchronous shader mode preserves completion waits");
 }
 flag_d3d12_nonblocking_pipeline_creation=flag_async_shader_compilation=true;
 {PipelineCache c;c.creation_threads_.clear();c.creation_threads_busy_=0;c.EndSubmission();check(!c.creation_completion_event_->waits,"no-worker configuration does not wait on an event");}
#if HAS_EXPLICIT_AWAIT
 {PipelineCache c;auto done=std::async(std::launch::async,[&]{c.AwaitPipelineCreation();});
  auto limit=std::chrono::steady_clock::now()+1s;
  while(!c.creation_completion_event_->waits&&std::chrono::steady_clock::now()<limit)std::this_thread::sleep_for(1ms);
  bool blocked=c.creation_completion_event_->waits&&done.wait_for(0ms)!=std::future_status::ready;
  c.FinishWorker();done.get();check(blocked,"cache lifetime boundary still joins active compiler work in async mode");
 }
#endif
 std::printf("%d failures\n",failures);return failures?1:0;
}
'''
Path(sys.argv[2]).write_text(('#define HAS_EXPLICIT_AWAIT '+str(int(bool(await_body)))+'\n')+template.replace('/*PRODUCTION_METHODS*/',body),encoding='utf-8')
