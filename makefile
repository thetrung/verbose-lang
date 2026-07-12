TARGET=prototype
default: clean build run

build: 
	dune build
	mv _build/default/bin/main.exe verbose.exe

run:
	./verbose.exe ${TARGET}.vb

demo: build
	# ./verbose.exe examples/*.vb
	./verbose.exe examples/enum.vb
	./verbose.exe examples/compute.vb
	./verbose.exe examples/loop_while.vb
	./verbose.exe examples/numberic.vb
	./verbose.exe examples/read_file.vb
	./verbose.exe examples/structure.vb
	./verbose.exe examples/nested_struct.vb
	rm -rf examples && git checkout examples

clean:
	rm -rf _build verbose.exe \
		${TARGET}.ll ${TARGET}.s ${TARGET}
