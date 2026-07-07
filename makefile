TARGET=prototype
default: clean build run

build: 
	dune build

run:
	# dune exec bin/main.exe -- ${TARGET} > res.ll && llc res.ll && clang res.s -o res && ./res
	dune exec bin/main.exe -- ${TARGET}.vb

clean:
	rm -rf _build ${TARGET}.ll ${TARGET}.s ${TARGET}
