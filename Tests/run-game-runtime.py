"""Run checks using the game's CLR and compatibility settings, without a GUI."""
import ctypes, json, os, sys
from pathlib import Path

root=Path(sys.argv[1]).resolve() if len(sys.argv)>1 else Path(__file__).resolve().parents[2]
game=Path(sys.argv[2]) if len(sys.argv)>2 else Path('D:/SteamLibrary/steamapps/common/RimWorld')
workshop=Path(sys.argv[3]) if len(sys.argv)>3 else Path('D:/SteamLibrary/steamapps/workshop/content/294100')
test=root/'Hold-Draft/Tests/bin/Validate.dll'
dirs=[game,test.parent,root/'Hold-Draft/1.7/Assemblies',workshop/'2009463077/1.7/Assemblies']
assemblies={}
for folder in dirs:
    for dll in folder.glob('*.dll'): assemblies.setdefault(dll.name.lower(),str(dll))
props={'TRUSTED_PLATFORM_ASSEMBLIES':';'.join(assemblies.values()),'APP_PATHS':';'.join(map(str,dirs)),'APP_CONTEXT_BASE_DIRECTORY':str(test.parent),'NATIVE_DLL_SEARCH_DIRECTORIES':str(game)}
config=json.loads((game/'RimWorld.runtimeconfig.json').read_text())['runtimeOptions']['configProperties']
props.update({k:str(v).lower() if isinstance(v,bool) else str(v) for k,v in config.items()})
dll_dir=os.add_dll_directory(str(game))
clr=ctypes.WinDLL(str(game/'coreclr.dll'))
keys=(ctypes.c_char_p*len(props))(*(k.encode() for k in props))
values=(ctypes.c_char_p*len(props))(*(v.encode() for v in props.values()))
handle=ctypes.c_void_p();domain=ctypes.c_uint()
init=clr.coreclr_initialize
init.argtypes=[ctypes.c_char_p,ctypes.c_char_p,ctypes.c_int,ctypes.POINTER(ctypes.c_char_p),ctypes.POINTER(ctypes.c_char_p),ctypes.POINTER(ctypes.c_void_p),ctypes.POINTER(ctypes.c_uint)]
init.restype=ctypes.c_int
result=init(str(game/'RimWorld.exe').encode(),b'GiantLifeValidation',len(props),keys,values,ctypes.byref(handle),ctypes.byref(domain))
if result<0: raise RuntimeError(f'coreclr initialization failed: {result:#x}')
run=clr.coreclr_execute_assembly
run.argtypes=[ctypes.c_void_p,ctypes.c_uint,ctypes.c_int,ctypes.POINTER(ctypes.c_char_p),ctypes.c_char_p,ctypes.POINTER(ctypes.c_uint)]
run.restype=ctypes.c_int
args=[str(root),str(game),str(workshop)]
argv=(ctypes.c_char_p*len(args))(*(a.encode() for a in args))
exitcode=ctypes.c_uint()
result=run(handle,domain,len(args),argv,str(test).encode(),ctypes.byref(exitcode))
if result<0: raise RuntimeError(f'coreclr execute failed: {result:#x}')
sys.exit(exitcode.value)
