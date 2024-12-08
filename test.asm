global _start;
section .text
_start: 
call _random_function 
mov rdi, 1
mov rsi, message;
mov rdx, message_length;

mov rax, 60
mov rdi, 0;

_random_function: 
push rbp
mov rbp, rsp
mov rax, 1
mov rsp, rbp
pop rbp
section .data
message: 
db 'Hello, World!', 
message_length equ $ - 
