SHELL := /bin/bash
.ONESHELL:
.DELETE_ON_ERROR:
MAKEFLAGS += --no-builtin-rules

VERSION ?= 0.0.1

ifeq ($(OS), Windows_NT)
    DETECTED_OS := Windows
    INSTALL_LOCATION ?= c:\dc-tools
else
    DETECTED_OS := $(shell sh -c 'uname 2>/dev/null || echo Unknown')
    INSTALL_LOCATION ?= /usr/local/bin
endif

.DEFAULT_GOAL := build

.PHONY: init
init:
ifeq ($(DETECTED_OS), Windows)
	choco install nodejs-lts
	choco install rktools.2003
endif

.PHONY: build
build:
	dotnet build ./src
	
.PHONY: publish
publish: clean
	dotnet publish ./src/DC.AWS.Projects.Cli -c Release --output $(CURDIR)/.out/win-x64 --self-contained -r win-x64 -p:PublishSingleFile=true
	dotnet publish ./src/DC.AWS.Projects.Cli -c Release --output $(CURDIR)/.out/linux-x64 --self-contained -r linux-x64 -p:PublishSingleFile=true

.PHONY: install
install: publish
ifeq ($(DETECTED_OS), Windows)
	if not exist $(INSTALL_LOCATION) mkdir $(INSTALL_LOCATION)
	pathman /au $(INSTALL_LOCATION)
	copy /B /Y .\.out\win-x64\dc.exe $(INSTALL_LOCATION)\dc.exe
else
	sudo cp ./.out/linux-x64/dc $(INSTALL_LOCATION)
endif

.PHONY: package
package: publish
	rm -rf ./.packages
	mkdir ./.packages
	$(foreach release, $(wildcard ./.out/*), cd $(release) && zip -x *.pdb -r ../../.packages/dc-$(VERSION)-$(notdir $(release)).zip . && cd $(CURDIR);)

.PHONY: clean
clean:
ifeq ($(DETECTED_OS), Windows)
	npx rimraf .\.packages
	npx rimraf .\.out
	npx rimraf .\**\**\obj
	npx rimraf .\**\**\bin
else
	rm -rf ./.packages
	rm -rf ./.out
	rm -rf ./**/**/obj
	rm -rf ./**/**/bin
endif