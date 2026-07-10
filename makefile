TARGET=prototype
default: clean build run

build: 
	dune build
	mv _build/default/bin/main.exe verbose.exe

run:
	# dune exec bin/main.exe -- ${TARGET} > res.ll && llc res.ll && clang res.s -o res && ./res
	# dune exec bin/main.exe -- ${TARGET}.vb
	verbose.exe ${TARGET}.vb

clean:
	rm -rf _build verbose.exe \
		${TARGET}.ll ${TARGET}.s ${TARGET}
