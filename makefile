TARGET=prototype
default: clean build run

build: 
	dune build
	mv _build/default/bin/main.exe verbose

run:
	./verbose ${TARGET}.vb

demo: build
	# ./verbose examples/*.vb
	./verbose examples/enum.vb
	./verbose examples/compute.vb
	./verbose examples/boolean.vb
	./verbose examples/byref.vb
	./verbose examples/loop_while.vb
	./verbose examples/numberic.vb
	./verbose examples/read_file.vb
	./verbose examples/structure.vb
	./verbose examples/structure2.vb
	./verbose examples/nested_struct.vb
	rm -rf examples && git checkout examples

clean:
	rm -rf _build \
		${TARGET}.ll ${TARGET}.s ${TARGET}
	@for dir in test/byref test/syntax; do \
		if [ -d "$$dir" ]; then \
			find "$$dir" -maxdepth 1 -type f ! -name '*.vb' -delete; \
		fi; \
	done
