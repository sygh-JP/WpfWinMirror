@rem for MSBuild
@rem "$(DXSDK_DIR)Utilities\bin\x86\fxc" GrayscaleEffect.hlsl /T ps_2_0 /Fo GrayscaleEffect.psbin
@rem 
@rem for Command Prompt
@echo off
"%DXSDK_DIR%Utilities\bin\x86\fxc" GrayscaleEffect.hlsl /T ps_2_0 /Fo GrayscaleEffect.psbin
"%DXSDK_DIR%Utilities\bin\x86\fxc" /nologo DarknessToOpacityEffect.hlsl /T ps_2_0 /Fo DarknessToOpacityEffect.psbin
"%DXSDK_DIR%Utilities\bin\x86\fxc" /nologo BrightnessToOpacityEffect.hlsl /T ps_2_0 /Fo BrightnessToOpacityEffect.psbin
"%DXSDK_DIR%Utilities\bin\x86\fxc" /nologo NPInvertEffect.hlsl /T ps_2_0 /Fo NPInvertEffect.psbin
@pause