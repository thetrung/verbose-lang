TARGET=prototype
default: clean build run

build: 
	dune build
	mv _build/default/bin/main.exe verbose.exe

run:
	./verbose.exe ${TARGET}.vb

clean:
	rm -rf _build verbose.exe \
		${TARGET}.ll ${TARGET}.s ${TARGET}
