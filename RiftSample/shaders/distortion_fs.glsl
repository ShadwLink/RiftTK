﻿#version 120

uniform vec2 LensCenter;
uniform vec2 ScreenCenter;
uniform vec2 Scale;
uniform vec2 ScaleIn;
uniform vec4 HmdWarpParam;
uniform sampler2D Texture0;
varying vec2 oTexCoord;

vec2 HmdWarp(vec2 in01)
{
	vec2  theta = (in01 - LensCenter) * ScaleIn; // Scales to [-1, 1]
	float rSq = theta.x * theta.x + theta.y * theta.y;
	vec2  theta1 = theta * (HmdWarpParam.x + HmdWarpParam.y * rSq + HmdWarpParam.z * rSq * rSq + HmdWarpParam.w * rSq * rSq * rSq);
	return LensCenter + Scale * theta1;
}

void main()
{
	vec2 tc = HmdWarp(oTexCoord);
    if (!all(equal(clamp(tc, ScreenCenter-vec2(0.25,0.5), ScreenCenter+vec2(0.25,0.5)), tc))) 
	{
		//gl_FragColor = vec4(0, 1, 0, 0.5);
		gl_FragColor = vec4(0);
	}
	else
	{
		gl_FragColor = texture2D(Texture0, tc);
	}
}