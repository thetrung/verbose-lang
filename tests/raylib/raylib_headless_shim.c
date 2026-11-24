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

typedef struct Vector3 {
    float x, y, z;
} Vector3;

typedef struct Rectangle {
    float x, y, width, height;
} Rectangle;

typedef struct Camera3D {
    Vector3 position;
    Vector3 target;
    Vector3 up;
    float fovy;
    int projection;
} Camera3D;

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

void BeginMode3D(Camera3D cam) {
    printf("Frame %d BeginMode3D(pos=%.1f,%.1f,%.1f tgt=%.1f,%.1f,%.1f up=%.1f,%.1f,%.1f fov=%.1f proj=%d)\n",
           g_frame,
           cam.position.x, cam.position.y, cam.position.z,
           cam.target.x, cam.target.y, cam.target.z,
           cam.up.x, cam.up.y, cam.up.z,
           cam.fovy, cam.projection);
}

void EndMode3D(void) {
    printf("Frame %d EndMode3D()\n", g_frame);
}

void DrawCube(Vector3 pos, float width, float height, float length, Color c) {
    printf("Frame %d DrawCube(%.1f,%.1f,%.1f sz=%.1f,%.1f,%.1f rgba=%u,%u,%u,%u)\n",
           g_frame, pos.x, pos.y, pos.z, width, height, length, c.r, c.g, c.b, c.a);
}

void DrawGrid(int slices, float spacing) {
    printf("Frame %d DrawGrid(%d,%.1f)\n", g_frame, slices, spacing);
}
