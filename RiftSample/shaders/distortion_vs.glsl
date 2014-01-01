#version 120

varying  vec2 oTexCoord;

void main()
{
	gl_Position = ftransform();
    gl_FrontColor = gl_Color;
    oTexCoord = gl_MultiTexCoord0.st;
}