SHELL := /bin/bash

.PHONY: build
build:
	cd ./src && dotnet build
	
.PHONY: publish
publish:
	rm -rf ./.out
	dotnet publish ./src/DC.AWS.Projects.Cli -c Release --output $(CURDIR)/.out --self-contained -r ubuntu.14.04-x64 -p:PublishSingleFile=true

.PHONY: install
install: publish
	sudo cp ./.out/dc-aws /usr/local/bin
	
.PHONY: clean
clean:
	rm -rf ./.out
	sudo rm -rf ./**/**/obj
	sudo rm -rf ./**/**/bin