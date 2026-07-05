TARGET=prototype.vb
default: clean build run

build: 
	dune build

run:
	dune exec bin/main.exe -- ${TARGET} > res.ll && llc res.ll && clang res.s -o res && ./res

clean:
	rm -rf _build res*
