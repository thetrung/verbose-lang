#include <stdio.h>
#include <stdint.h>

struct Vector {
  float x;
  double y;
  long z;
};

enum NodeType {
  Literal = 1,
  BinaryOp = 2,
  FunctionCall = 3
};

struct Mix {
  struct Vector vec;
  enum NodeType node;
};

int main (){
  struct Vector vec1 = { 1.0, 2.00, 9999999 };
  printf("vec1@%lu:\n (%f -- %f -- %ld)\n", (uintptr_t)&vec1, vec1.x, vec1.y, vec1.z);


  enum NodeType node = Literal;
  printf("nodeType = %d\n", node);

  struct Mix mix = { vec1, 0 };
  mix.node = Literal;
  printf("mix:\n .vec = @%lu\n", (uintptr_t)&mix.vec);
  printf("    .x single %f\n", mix.vec.x);
  printf("    .y double %f\n", mix.vec.y);
  printf("    .z long   %ld\n", mix.vec.z);
  printf(" .node integer %d\n",mix.node);
  return 0;
}
