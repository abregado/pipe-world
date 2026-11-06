#ifndef PIPEWORLD_FADE_INCLUDED
#define PIPEWORLD_FADE_INCLUDED

// Current draw distances, set by ScatterCulling every refresh (they shrink toward their night values with the fog).
// Tiles switch off by the distance to their nearest point, so a surface that has faded out by its own distance
// is already invisible when its tile goes.
float _PW_ScatterDrawDistance;
float _PW_GrassDrawDistance;

// 1 near the camera, 0 from 95% of the scatter draw distance; the fade starts at 70%.
float PW_ScatterFade(float3 positionWS)
{
    float draw = max(_PW_ScatterDrawDistance, 1.0);
    return 1.0 - smoothstep(draw * 0.7, draw * 0.95, distance(positionWS, _WorldSpaceCameraPos));
}

// Screen-space dither, so a fading surface dissolves instead of turning transparent.
float PW_Dither(float2 pixel)
{
    return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
}

// Discards a dithered share of the pixels as fade drops below 1. Fully visible surfaces are never clipped: an offset
// in the comparison would cut every pixel whose dither value is near 1, a sprinkle of holes over the whole surface.
void PW_DitherClip(float fade, float2 pixel)
{
    if (fade < 1.0)
        clip(fade - PW_Dither(pixel) - 0.001);
}

#endif
