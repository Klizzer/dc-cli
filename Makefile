SHELL := /bin/bash
.ONESHELL:
.DELETE_ON_ERROR:
MAKEFLAGS += --no-builtin-rules

ifeq ($(OS), Windows_NT)
    DETECTED_OS := Windows
    INSTALL_LOCATION ?= 'c:\dc-tools'
else
    DETECTED_OS := $(shell sh -c 'uname 2>/dev/null || echo Unknown')
    INSTALL_LOCATION ?= /usr/local/bin
endif

.DEFAULT_GOAL := build

.PHONY: build
build:
	cd ./src && dotnet build
	
.PHONY: publish
publish: clean
ifeq (DETECTED_OS, Windows)
	dotnet publish ./src/DC.AWS.Projects.Cli -c Release --output $(CURDIR)/.out --self-contained -r win-x64 -p:PublishSingleFile=true
else
	dotnet publish ./src/DC.AWS.Projects.Cli -c Release --output $(CURDIR)/.out --self-contained -r linux-x64 -p:PublishSingleFile=true
endif

.PHONY: install
install: publish
ifeq (DETECTED_OS, Windows)
	if not exist $(INSTALL_LOCATION) mkdir $(INSTALL_LOCATION)
	pathman /au $(INSTALL_LOCATION)
	copy /B /Y ./.out/dc-aws.exe $(INSTALL_LOCATION)/dc-aws.exe
else
	sudo cp ./.out/dc-aws $(INSTALL_LOCATION)
endif

.PHONY: clean
clean:
ifeq (DETECTED_OS, Windows)
	rmdir /Q /S ./.out
	rmdir /Q /S ./**/**/obj
	rmdir /Q /S ./**/**/bin
else
	rm -rf ./.out
	rm -rf ./**/**/obj
	rm -rf ./**/**/bin
endif