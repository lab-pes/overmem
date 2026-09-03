return [====[
{ Game   : PES2021.exe
  Version:
  Date   : 2020-09-16
  Author : Aranaktu

  This script does blah blah blah
}

[ENABLE]
//code from here to '[DISABLE]' will be used to enable the cheat



aobscanmodule(INJECT_ptrPlayer,PES2021.exe,CB B8 02 00 00 00 0F 1F 40 00 66 0F 1F 84 00 00 00 00 00 0F 10 02 0F 11 01) // should be unique
alloc(newmem,$1000,"PES2021.exe"+C650F0)

alloc(ptrPlayer, 8, "PES2021.exe"+C650F0)
registersymbol(ptrPlayer)
ptrPlayer:
dq 00

label(code)
label(return)

newmem:
  cmp eax, 2
  jne code
  mov [ptrPlayer], rdx

code:
  movups xmm0,[rdx]
  movups [rcx],xmm0
  jmp return

INJECT_ptrPlayer+13:
  jmp newmem
  nop
return:
registersymbol(INJECT_ptrPlayer)

aobscanmodule(INJECT_ptrPlayerTwo,PES2021.exe,48 8B 40 2C 48 89 02) // should be unique
alloc(newmem_ptrPlayerTwo,$1000,"PES2021.exe"+730FFA7)

label(code_ptrPlayerTwo)
label(ret_ptrPlayerTwo)

newmem_ptrPlayerTwo:
  mov [ptrPlayer], rax

code_ptrPlayerTwo:
  mov rax,[rax+2C]
  mov [rdx],rax
  jmp ret_ptrPlayerTwo

INJECT_ptrPlayerTwo:
  jmp newmem_ptrPlayerTwo
  nop
  nop
ret_ptrPlayerTwo:
registersymbol(INJECT_ptrPlayerTwo)

[DISABLE]
//code from here till the end of the code will be used to disable the cheat
INJECT_ptrPlayer+13:
  db 0F 10 02 0F 11 01

unregistersymbol(INJECT_ptrPlayer)
dealloc(newmem)

//code_ptrPlayerTwo from here till the end of the code_ptrPlayerTwo will be used to disable the cheat
INJECT_ptrPlayerTwo:
  db 48 8B 40 2C 48 89 02

unregistersymbol(INJECT_ptrPlayerTwo)
dealloc(newmem_ptrPlayerTwo)

unregistersymbol(ptrPlayer)
dealloc(ptrPlayer)

{
// ORIGINAL CODE - INJECT_ptrPlayerION POINT: "PES2021.exe"+C650F0

"PES2021.exe"+C650C3: 90                          -  nop
"PES2021.exe"+C650C4: E9 6B 03 00 00              -  jmp PES2021.exe+C65434
"PES2021.exe"+C650C9: 48 8B 96 98 00 00 00        -  mov rdx,[rsi+00000098]
"PES2021.exe"+C650D0: 48 8B CF                    -  mov rcx,rdi
"PES2021.exe"+C650D3: E8 F8 AE 81 00              -  call PES2021.exe+147FFD0
"PES2021.exe"+C650D8: 48 8B D0                    -  mov rdx,rax
"PES2021.exe"+C650DB: 48 8B CB                    -  mov rcx,rbx
"PES2021.exe"+C650DE: B8 02 00 00 00              -  mov eax,00000002
"PES2021.exe"+C650E3: 0F 1F 40 00                 -  nop [rax+00]
"PES2021.exe"+C650E7: 66 0F 1F 84 00 00 00 00 00  -  nop [rax+rax+00000000]
// ---------- INJECT_ptrPlayerING HERE ----------
"PES2021.exe"+C650F0: 0F 10 02                    -  movups xmm0,[rdx]
"PES2021.exe"+C650F3: 0F 11 01                    -  movups [rcx],xmm0
// ---------- DONE INJECT_ptrPlayerING  ----------
"PES2021.exe"+C650F6: 0F 10 4A 10                 -  movups xmm1,[rdx+10]
"PES2021.exe"+C650FA: 0F 11 49 10                 -  movups [rcx+10],xmm1
"PES2021.exe"+C650FE: 0F 10 42 20                 -  movups xmm0,[rdx+20]
"PES2021.exe"+C65102: 0F 11 41 20                 -  movups [rcx+20],xmm0
"PES2021.exe"+C65106: 0F 10 4A 30                 -  movups xmm1,[rdx+30]
"PES2021.exe"+C6510A: 0F 11 49 30                 -  movups [rcx+30],xmm1
"PES2021.exe"+C6510E: 0F 10 42 40                 -  movups xmm0,[rdx+40]
"PES2021.exe"+C65112: 0F 11 41 40                 -  movups [rcx+40],xmm0
"PES2021.exe"+C65116: 0F 10 4A 50                 -  movups xmm1,[rdx+50]
"PES2021.exe"+C6511A: 0F 11 49 50                 -  movups [rcx+50],xmm1
}


{
// ORIGINAL code_ptrPlayerTwo - INJECT_ptrPlayerTwoION POINT: "PES2021.exe"+730FFA7

"PES2021.exe"+730FF86: FF E7                 -  jmp rdi
"PES2021.exe"+730FF88: C6                    -  db -3A
"PES2021.exe"+730FF89: 0F 1F 80 00 00 00 00  -  nop [rax+00000000]
"PES2021.exe"+730FF90: 48 8B 41 08           -  mov rax,[rcx+08]
"PES2021.exe"+730FF94: 48 85 C0              -  test rax,rax
"PES2021.exe"+730FF97: 75 0E                 -  jne PES2021.exe+730FFA7
"PES2021.exe"+730FF99: 48 8B 05 40 FE 17 FC  -  mov rax,[PES2021.exe+348FDE0]
"PES2021.exe"+730FFA0: 48 89 02              -  mov [rdx],rax
"PES2021.exe"+730FFA3: 48 89 D0              -  mov rax,rdx
"PES2021.exe"+730FFA6: C3                    -  ret
// ---------- INJECT_ptrPlayerTwoING HERE ----------
"PES2021.exe"+730FFA7: 48 8B 40 2C           -  mov rax,[rax+2C]
"PES2021.exe"+730FFAB: 48 89 02              -  mov [rdx],rax
// ---------- DONE INJECT_ptrPlayerTwoING  ----------
"PES2021.exe"+730FFAE: 48 89 D0              -  mov rax,rdx
"PES2021.exe"+730FFB1: C3                    -  ret
"PES2021.exe"+730FFB2: 4C 8B 1C 24           -  mov r11,[rsp]
"PES2021.exe"+730FFB6: 4D 89 DD              -  mov r13,r11
"PES2021.exe"+730FFB9: 48 8D 64 24 08        -  lea rsp,[rsp+08]
"PES2021.exe"+730FFBE: 4C 8B 04 24           -  mov r8,[rsp]
"PES2021.exe"+730FFC2: 48 83 EC F8           -  sub rsp,-08
"PES2021.exe"+730FFC6: E9 45 F8 5D 0E        -  jmp PES2021.exe+158EF810
"PES2021.exe"+730FFCB: 48 31 D2              -  xor rdx,rdx
"PES2021.exe"+730FFCE: 56                    -  push rsi
}

]====]
