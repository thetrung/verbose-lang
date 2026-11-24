#include <stdint.h>
#include <stdio.h>

typedef struct BigBox {
    int64_t a, b, c;
} BigBox;

int64_t sum_big_byval(BigBox box) {
    return box.a + box.b + box.c;
}

void scale_big_byref(BigBox *box, int64_t k) {
    box->a *= k;
    box->b *= k;
    box->c *= k;
}
