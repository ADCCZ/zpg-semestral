#version 330 core
layout(location = 0) in vec2 pos;
uniform mat4 projection;
uniform mat4 view;
uniform mat4 model;
uniform vec3 color;
out vec3 fragColor;
void main() {
    fragColor = color;
    gl_Position = projection * view * model * vec4(pos, 0.0, 1.0);
}