#version 120

uniform vec2 LensCenter;
uniform vec2 ScreenCenter;
uniform vec2 Scale;
uniform vec2 ScaleIn;
uniform vec4 HmdWarpParam;
uniform sampler2D Texture0;
varying vec2 oTexCoord;

void main()
{
	gl_FragColor = texture2D(Texture0, oTexCoord);
}