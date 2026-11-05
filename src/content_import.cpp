// Isolated, data-only STFS importer. The launcher commits the staged result.
#include <windows.h>
#include <algorithm>
#include <array>
#include <cwctype>
#include <filesystem>
#include <fstream>
#include <memory>
#include <set>
#include <stdexcept>
#include <vector>
#include "dlc_song_catalog.h"
#include <rex/logging.h>
#include <rex/filesystem/entry.h>
#include <rex/filesystem/file.h>
#include <rex/filesystem/devices/stfs_container_device.h>
#include <rex/system/xam/content_manager.h>

namespace fs=std::filesystem;
using namespace rex::filesystem;
constexpr uint32_t kTitle=0x454108B1;
constexpr uint32_t kGenericCustomTitle=0xFFFE07D1;
static fs::path LongPath(const fs::path& path){auto s=fs::absolute(path).wstring();if(s.starts_with(L"\\\\?\\"))return fs::path(s);return fs::path(s.starts_with(L"\\\\")?L"\\\\?\\UNC\\"+s.substr(2):L"\\\\?\\"+s);}
struct Report {bool ok=false;std::string name,error;uint64_t bytes=0;size_t files=0;};
static std::string Json(const std::string& text){
 std::string out="\"";for(unsigned char c:text){if(c=='"'||c=='\\'){out+='\\';out+=c;}else if(c<32){const char*h="0123456789ABCDEF";out+="\\u00";out+=h[c>>4];out+=h[c&15];}else out+=c;}return out+'"';
}
static void WriteReport(const fs::path& path,const Report&r){std::ofstream f(path,std::ios::binary);f<<"{\"Ok\":"<<(r.ok?"true":"false")<<",\"Name\":"<<Json(r.name)<<",\"Error\":"<<Json(r.error)<<",\"Files\":"<<r.files<<",\"Bytes\":"<<r.bytes<<"}";if(!f)throw std::runtime_error("Could not write the import result.");}
static std::wstring Lower(std::wstring s){std::transform(s.begin(),s.end(),s.begin(),[](wchar_t c){return std::towlower(c);});return s;}
static fs::path SafeName(const std::string& name){
 auto p=rex::to_path(name);auto s=p.wstring();
 if(s.empty()||s==L"."||s==L".."||s.size()>120||s.back()==L'.'||s.back()==L' ')throw std::runtime_error("The package contains an invalid file name.");
 for(auto c:s)if(c<32||std::wstring(L"\\/:*?\"<>|").find(c)!=std::wstring::npos)throw std::runtime_error("The package contains an unsafe file path.");
 auto stem=Lower(s.substr(0,s.find(L'.')));
 if(stem==L"con"||stem==L"prn"||stem==L"aux"||stem==L"nul"||(stem.size()==4&&(stem.starts_with(L"com")||stem.starts_with(L"lpt"))&&stem[3]>=L'1'&&stem[3]<=L'9'))throw std::runtime_error("The package contains a reserved Windows file name.");
 return p;
}
struct Item {Entry* entry;fs::path relative;};
static std::vector<uint8_t> ReadEntry(Entry* entry,size_t limit){
 if(!entry||entry->size()>limit)throw std::runtime_error("The custom song catalog is missing or too large.");
 File* raw=nullptr;if(entry->Open(FileAccess::kFileReadData,&raw)!=0||!raw)throw std::runtime_error("Could not read custom song metadata.");
 auto file=std::unique_ptr<File,void(*)(File*)>(raw,[](File*f){f->Destroy();});
 std::vector<uint8_t> data(entry->size());size_t offset=0;
 while(offset<data.size()){size_t count=0;auto status=file->ReadSync(std::span<uint8_t>(data).subspan(offset),offset,&count);if(status!=0||!count||count>data.size()-offset)throw std::runtime_error("Custom song metadata is incomplete.");offset+=count;}
 return data;
}
static void ValidateGenericSongs(const std::vector<Item>& items){
 auto find=[&](const std::string& path)->Entry*{auto expected=Lower(rex::to_path(path).generic_wstring());for(const auto& item:items)if(!(item.entry->attributes()&kFileAttributeDirectory)&&Lower(item.relative.generic_wstring())==expected)return item.entry;return nullptr;};
 auto* catalog=find("songs/songs.dta");auto bytes=ReadEntry(catalog,1024*1024);
 auto stems=beatles::dlc::CustomSongStems(std::string_view(reinterpret_cast<const char*>(bytes.data()),bytes.size()));
 for(const auto& stem:stems){
  auto* midi=find(stem+".mid");auto* audio=find(stem+".mogg");
  auto path=fs::path(stem);auto visual=path.parent_path()/"gen"/(path.filename().string()+".milo_xbox");
  auto* scene=find(visual.generic_string());
  if(!midi||!audio||!scene||midi->size()<4||!audio->size()||!scene->size())throw std::runtime_error("The generic-title package is missing TBRB chart, audio or Milo assets.");
  // Only read the MIDI signature, not the complete chart or any audio.
  File* raw=nullptr;if(midi->Open(FileAccess::kFileReadData,&raw)!=0||!raw)throw std::runtime_error("Could not open the custom song chart.");
  auto file=std::unique_ptr<File,void(*)(File*)>(raw,[](File*f){f->Destroy();});std::array<uint8_t,4> magic{};size_t count=0;
  if(file->ReadSync(magic,0,&count)!=0||count!=4||std::string_view(reinterpret_cast<const char*>(magic.data()),4)!="MThd")throw std::runtime_error("The custom song chart is not a MIDI file.");
 }
}
static std::vector<Item> InspectTree(Entry* root,Report&r){
 std::vector<Item> items{{root,{}}};std::set<std::wstring> names;
 for(size_t i=0;i<items.size();++i){auto item=items[i];
  for(auto&child:item.entry->children()){
   auto relative=item.relative/SafeName(child->name());
   if(relative.wstring().size()>220||!names.insert(Lower(relative.wstring())).second)throw std::runtime_error("The package contains duplicate or excessively long paths.");
   auto extension=Lower(relative.extension().wstring());
   if(extension==L".xex"||extension==L".exe"||extension==L".dll")throw std::runtime_error("This package includes executable code. Only data-only DLC is supported.");
   if(!(child->attributes()&kFileAttributeDirectory)){
    if(child->size()>32ull*1024*1024*1024-r.bytes)throw std::runtime_error("The package is too large.");
    r.bytes+=child->size();++r.files;
   }
   items.push_back({child.get(),relative});if(items.size()>100000)throw std::runtime_error("The package contains too many files.");
  }
 }
 if(!r.files)throw std::runtime_error("The package contains no DLC files.");return items;
}
static void Extract(const Item&item,const fs::path&root){
 auto path=root/item.relative;
 if(item.entry->attributes()&kFileAttributeDirectory){fs::create_directories(path);return;}
 fs::create_directories(path.parent_path());if(fs::exists(path))throw std::runtime_error("Duplicate file in package.");
 File* raw=nullptr;
 if(item.entry->Open(FileAccess::kFileReadData,&raw)!=0||!raw)throw std::runtime_error("Could not read a file in this package.");
 auto file=std::unique_ptr<File,void(*)(File*)>(raw,[](File*f){f->Destroy();});
 std::ofstream out(path,std::ios::binary);if(!out)throw std::runtime_error("Could not create a DLC file. Check free space and folder access.");
 std::vector<uint8_t> buffer(1024*1024);size_t offset=0;
 while(offset<item.entry->size()){
  size_t count=0,wanted=std::min(buffer.size(),item.entry->size()-offset);
  auto status=file->ReadSync(std::span<uint8_t>(buffer.data(),wanted),offset,&count);
  if(status!=0||!count||count>wanted)throw std::runtime_error("The package is incomplete or damaged.");
  if(!offset&&count>=4&&std::equal(buffer.begin(),buffer.begin()+4,reinterpret_cast<const uint8_t*>("XEX2")))throw std::runtime_error("This package contains executable code and cannot be imported as song data.");
  out.write(reinterpret_cast<const char*>(buffer.data()),count);if(!out)throw std::runtime_error("Could not write DLC data. Check free disk space.");offset+=count;
 }
 out.close();if(!out)throw std::runtime_error("Could not finish writing DLC data.");
}
int wmain(int argc,wchar_t**argv){
 SetErrorMode(SEM_FAILCRITICALERRORS|SEM_NOGPFAULTERRORBOX);if(argc!=5)return 2;
 Report report;
 try{
  rex::InitLogging({},spdlog::level::off);
  fs::path source=LongPath(argv[1]),stage=LongPath(argv[2]);std::wstring id=argv[3];
  if(id.size()!=40||id.find_first_not_of(L"0123456789ABCDEF")!=std::wstring::npos)throw std::runtime_error("Invalid package identity.");
  if(fs::exists(stage))throw std::runtime_error("The staging folder must be new.");
  auto header=StfsContainerDevice::ReadPackageHeader(source);
  if(!header)throw std::runtime_error("Choose an original Xbox 360 STFS DLC package (LIVE, PIRS or CON), not a ZIP, RAR or XEX.");
  const uint32_t sourceTitle=header->metadata.execution_info.title_id;
  if(sourceTitle!=kTitle&&sourceTitle!=kGenericCustomTitle)throw std::runtime_error(fmt::format("This package declares title ID {:08X}; TBRB requires 454108B1. Only verified TBRB customs may use generic title FFFE07D1.",sourceTitle));
  if(header->metadata.content_type!=rex::system::XContentType::kMarketplaceContent)throw std::runtime_error("This is not a downloadable-content package. Saves, disc images and title updates cannot be imported here.");
  if(header->metadata.volume_type!=XContentVolumeType::kStfs||header->metadata.data_file_count>1||!header->metadata.volume_descriptor.stfs.is_valid())throw std::runtime_error("Unsupported or damaged STFS volume.");
  auto device=std::make_unique<StfsContainerDevice>("",source);
  if(!device->Initialize())throw std::runtime_error("Could not read this DLC package. It may be incomplete or damaged.");
  auto root=device->ResolvePath("");if(!root)throw std::runtime_error("The DLC package has no file table.");
  auto title=header->metadata.display_name(rex::system::XLanguage::kEnglish);report.name=title.empty()?rex::path_to_utf8(source.filename()):rex::string::to_utf8(title);
  auto items=InspectTree(root,report);
  if(sourceTitle==kGenericCustomTitle)ValidateGenericSongs(items);
  fs::create_directories(stage);if(fs::space(stage).available<report.bytes+16*1024*1024)throw std::runtime_error("There is not enough free space to install this DLC.");
  rex::system::xam::ContentManager manager(nullptr,stage);
  rex::system::xam::XCONTENT_AGGREGATE_DATA data{};data.device_id=1;data.content_type=rex::system::XContentType::kMarketplaceContent;data.title_id=kTitle;data.xuid=0;data.set_file_name(rex::path_to_utf8(fs::path(id)));data.set_display_name(rex::string::to_utf16(report.name));
  auto package=stage/L"0000000000000000"/L"454108B1"/L"00000002"/id;fs::create_directories(package);
  for(auto&item:items)Extract(item,package);
  uint32_t mask=0;for(const auto&license:header->header.licenses)if(license.license_flags)mask|=license.license_bits;
  if(manager.WriteContentHeaderFile(0,data,mask)!=0)throw std::runtime_error("Could not save DLC metadata.");
  auto listed=manager.ListContent(1,0,rex::system::XContentType::kMarketplaceContent,kTitle);
  if(listed.size()!=1||listed[0].file_name()!=data.file_name())throw std::runtime_error("The SDK could not enumerate the installed package.");
  report.ok=true;
 }catch(const std::exception&e){report.error=e.what();}
 try{WriteReport(LongPath(argv[4]),report);}catch(...){return 3;}
 return report.ok?0:1;
}
