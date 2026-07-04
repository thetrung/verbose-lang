default: build run clean

build: 
	dune build

run:
	./_build/default/bin/main.exe

clean:
	rm -rf _build
