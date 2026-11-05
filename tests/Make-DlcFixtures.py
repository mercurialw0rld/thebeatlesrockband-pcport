from pathlib import Path
import struct,sys
root=Path(sys.argv[1]) if len(sys.argv)>1 else Path(__file__).parent/'fixtures';root.mkdir(parents=True,exist_ok=True)
def make(directory='songs',second='meta.bin',title=0x454108B1,kind=2):
 b=bytearray(0xe000);b[:4]=b'LIVE'
 def be(offset,value):struct.pack_into('>I',b,offset,value)
 be(0x340,0x971a);be(0x344,kind);be(0x348,2);be(0x360,title)
 b[0x379]=0x24;b[0x37b]=1;struct.pack_into('<H',b,0x37c,1);be(0x395,3)
 text='Importer fixture (not a song)'.encode('utf-16-be');b[0x411:0x411+len(text)]=text
 for i in range(3):be(0xa000+i*24+20,0x80ffffff)
 def entry(index,name,parent,block,payload=None):
  o=0xb000+index*64;n=name.encode();b[o:o+len(n)]=n;b[o+40]=len(n)|(0x80 if payload is None else 0x40);struct.pack_into('>H',b,o+50,parent)
  if payload is not None:
   b[o+41:o+44]=(1).to_bytes(3,'little');b[o+44:o+47]=(1).to_bytes(3,'little');b[o+47:o+50]=block.to_bytes(3,'little');be(o+52,len(payload));p=0xb000+block*0x1000;b[p:p+len(payload)]=payload
 entry(0,directory,0xffff,0);entry(1,'songs.dta',0,1,b'; synthetic importer test data only\n');entry(2,second,0xffff,2,b'not commercial game data\n')
 return b
fixtures={'good':make(),'wrong-title':make(title=0x12345678),'save-game':make(kind=1),'unsafe-path':make(directory='..'),'executable':make(second='code.xex'),'truncated':make()[:0xd002],'bad-magic':b'PK\x03\x04not a DLC package'}
for name,data in fixtures.items():(root/name).write_bytes(data)
print('Wrote',len(fixtures),'synthetic fixtures:',root)

def custom(origin='beatles',title=0xFFFE07D1,midi=b'MThd\x00\x00\x00\x06\x00\x01\x00\x00\x01\xe0',milo='custom.milo_xbox',catalog=None):
 b=bytearray(0x10000);b[:4]=b'LIVE'
 def be(offset,value):struct.pack_into('>I',b,offset,value)
 be(0x340,0x971a);be(0x344,2);be(0x348,2);be(0x360,title)
 b[0x379]=0x24;b[0x37b]=1;struct.pack_into('<H',b,0x37c,1);be(0x395,5)
 name='Synthetic TBRB custom'.encode('utf-16-be');b[0x411:0x411+len(name)]=name
 for i in range(5):be(0xa000+i*24+20,0x80ffffff)
 def entry(index,name,parent,block,payload=None):
  o=0xb000+index*64;n=name.encode();b[o:o+len(n)]=n;b[o+40]=len(n)|(0x80 if payload is None else 0x40);struct.pack_into('>H',b,o+50,parent)
  if payload is not None:
   b[o+41:o+44]=(1).to_bytes(3,'little');b[o+44:o+47]=(1).to_bytes(3,'little');b[o+47:o+50]=block.to_bytes(3,'little');be(o+52,len(payload));p=0xb000+block*0x1000;b[p:p+len(payload)]=payload
 dta=catalog or ('(custom (name "Synthetic test") (game_origin '+origin+') (song (name songs/custom/custom)))').encode()
 entry(0,'songs',0xffff,0);entry(1,'custom',0,0);entry(2,'gen',1,0)
 entry(3,'songs.dta',0,1,dta);entry(4,'custom.mid',1,2,midi);entry(5,'custom.mogg',1,3,b'synthetic, not playable audio');entry(6,milo,2,4,b'synthetic, not playable Milo')
 return b
custom_fixtures={
 'generic-good':custom(),
 'generic-other-origin':custom(origin='rb3'),
 'other-title-beatles-catalog':custom(title=0x45410914),
 'generic-missing-milo':custom(milo='missing.bin'),
 'generic-bad-midi':custom(midi=b'NOPE'),
 'generic-comment-spoof':custom(catalog=b'; (fake (game_origin beatles))\n(custom (song (name songs/custom/custom)))'),
 'generic-string-spoof':custom(catalog=b'(custom (name "(game_origin beatles)") (song (name songs/custom/custom)))'),
}
for name,data in custom_fixtures.items():(root/name).write_bytes(data)
print('Wrote',len(custom_fixtures),'synthetic custom validation fixtures')
