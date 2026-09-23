#version 450 core

struct Material {
    vec3 ambient;
    vec3 diffuse;
    vec3 specular;
    float shininess;
};
uniform Material material;

uniform vec3 cameraPosWorld;
uniform vec3 lightPosWorld;
uniform vec3 lightDirWorld;

uniform vec3 lightColor;
uniform float lightIntensity;

uniform int lightOn;
uniform float cutOff;

in vec3 normalWorld;
in vec3 fragmentWorld;

out vec4 outColor;

void main() {
    vec3 norm = normalize(normalWorld);
    vec3 ambient = material.diffuse * lightColor * 0.1;
    vec3 finalColor;

    vec3 lightDir = normalize(lightPosWorld.xyz - fragmentWorld);

    float theta = dot(lightDir, normalize(lightDirWorld));

    float diff = max(dot(norm, normalize(lightDir)), 0.0);
    vec3 diffuse = material.diffuse * lightColor * diff * 0.2;

    vec3 viewDir = normalize(cameraPosWorld - fragmentWorld);
    vec3 reflectDir = reflect(-lightDir.xyz, norm);
    vec3 specular = material.specular * pow(max(dot(reflectDir, viewDir), 0.0), material.shininess) * lightColor * lightIntensity;

    float intensity = smoothstep(cutOff - 0.1, cutOff, theta);
    vec3 ambientSmooth = mix(ambient * 3.0, ambient * 6.0, intensity);
    vec3 specularSmooth = mix(vec3(0.0), specular, intensity);

    finalColor = diffuse + ambientSmooth + specularSmooth * 1.5;

    if(lightOn == 0)
    {
        finalColor = ambient + diffuse;
    }
    else
    {
        finalColor = diffuse + ambientSmooth + specularSmooth * 0.5;
    }

    outColor = vec4(finalColor, 1.0);
}