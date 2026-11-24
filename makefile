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
	./verbose examples/arrays.vb
	./verbose examples/static_heap.vb
	./verbose examples/arena.vb
	rm -rf examples && git checkout examples

test: build
	python3 test/run_syntax_tests.py
	python3 test/run_byref_tests.py
	python3 test/run_array_tests.py
	python3 test/run_static_heap_tests.py
	python3 test/run_arena_tests.py
	python3 test/run_ta_tests.py
	python3 test/run_module_tests.py
	python3 test/run_c_interop_tests.py
	python3 test/run_bootstrap_tests.py

clean:
	rm -rf _build \
		${TARGET}.ll ${TARGET}.s ${TARGET} \
		bootstrap/*.ll bootstrap/vbc \
		examples/raylib/*.ll examples/raylib/bouncing_ball
	@for dir in test/byref test/syntax test/arrays test/static_heap test/arena test/type_analyzer test/modules test/c_interop; do \
		if [ -d "$$dir" ]; then \
			find "$$dir" -maxdepth 1 -type f ! -name '*.vb' ! -name '*.c' -delete; \
		fi; \
	done
