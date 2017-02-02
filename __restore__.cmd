@echo off

pushd %~dp0
setlocal

mkdir .paket
mkdir packages

curl -L https://github.com/fsprojects/Paket/releases/download/3.31.8/paket.bootstrapper.exe -o .paket\paket.bootstrapper.exe
curl -L https://dist.nuget.org/win-x86-commandline/latest/nuget.exe -o packages\nuget.exe

.paket\paket.bootstrapper.exe
.paket\paket init
.paket\paket add nuget fake
.paket\paket restore

endlocal
popd
