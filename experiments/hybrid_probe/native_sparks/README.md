# Native Sparks C++ proof of concept

This directory contains a Windows x86 C++ DLL that reimplements Sparks using the native damage command producer. The original Joey executable remains unchanged. Compile the DLL for Win32 and use the separate Frida host to load and restore only Sparks' primary callback pointer. The project is experimental and requires verification in the game.
