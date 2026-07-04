default: build run clean

build: 
	dune build

run:
	./_build/default/bin/main.exe

# test:
# 	dune exec bin/main.exe -- test.vb > res.ll && llc res.ll && clang res.s -o res && ./res
# 	echo $?

clean:
	rm -rf _build
