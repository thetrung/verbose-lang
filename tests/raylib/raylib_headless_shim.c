/* Headless C ABI verification shim matching real Raylib 5.x struct layouts & signatures */
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>

typedef struct Color {
    uint8_t r, g, b, a;
} Color;

typedef struct Vector2 {
    float x, y;
} Vector2;

typedef struct Rectangle {
    float x, y, width, height;
} Rectangle;

static int g_frame = 0;

void InitWindow(int width, int height, const char *title) {
    printf("InitWindow(%d,%d,%s)\n", width, height, title);
}

void CloseWindow(void) {
    printf("CloseWindow(frames=%d)\n", g_frame);
}

void SetTargetFPS(int fps) {
    printf("SetTargetFPS(%d)\n", fps);
}

bool WindowShouldClose(void) {
    return g_frame >= 3;
}

float GetFrameTime(void) {
    return 0.5f;
}

void BeginDrawing(void) {
    g_frame++;
}

void EndDrawing(void) {}

void ClearBackground(Color c) {
    printf("Frame %d ClearBackground(%u,%u,%u,%u)\n", g_frame, c.r, c.g, c.b, c.a);
}

void DrawCircleV(Vector2 center, float radius, Color c) {
    printf("Frame %d DrawCircleV(%.1f,%.1f r=%.1f rgba=%u,%u,%u,%u)\n",
           g_frame, center.x, center.y, radius, c.r, c.g, c.b, c.a);
}

void DrawRectangleRec(Rectangle rec, Color c) {
    printf("Frame %d DrawRectangleRec(%.1f,%.1f,%.1f,%.1f rgba=%u,%u,%u,%u)\n",
           g_frame, rec.x, rec.y, rec.width, rec.height, c.r, c.g, c.b, c.a);
}

void DrawText(const char *text, int posX, int posY, int fontSize, Color c) {
    printf("Frame %d DrawText(%s @%d,%d sz=%d rgba=%u,%u,%u,%u)\n",
           g_frame, text, posX, posY, fontSize, c.r, c.g, c.b, c.a);
}

Vector2 GetMousePosition(void) {
    Vector2 m = { 200.0f + (float)g_frame * 25.0f, 300.0f };
    return m;
}

bool IsKeyDown(int key) {
    return key == 32;
}
