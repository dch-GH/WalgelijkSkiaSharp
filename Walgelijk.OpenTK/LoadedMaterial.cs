﻿using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;

namespace Walgelijk.OpenTK
{
    public class LoadedMaterial
    {
        public int ProgramHandle { get; private set; }

        public Material Material { get; private set; }

        private readonly Dictionary<string, int> uniformLocations = new Dictionary<string, int>();
        private readonly Dictionary<int, int> textureUnitAssignments = new Dictionary<int, int>();

        public LoadedMaterial(Material material)
        {
            SetFromMaterial(material);
        }

        private void SetFromMaterial(Material material)
        {
            var shader = material.Shader;
            int vertexShaderIndex;
            int fragmentShaderIndex;
            int programIndex;

            try
            {
                CreateShaderProgram(
                    shader,
                    out vertexShaderIndex,
                    out fragmentShaderIndex,
                    out programIndex);
            }
            catch (Exception)
            {
                throw;
            }

            LinkShaders(vertexShaderIndex, fragmentShaderIndex, programIndex);

            GL.ValidateProgram(programIndex);

            ReleaseShaders(vertexShaderIndex, fragmentShaderIndex, programIndex);

            Material = material;
            ProgramHandle = programIndex;
        }

        private static void ReleaseShaders(int vertexShaderIndex, int fragmentShaderIndex, int programIndex)
        {
            GL.DeleteShader(vertexShaderIndex);
            GL.DeleteShader(fragmentShaderIndex);

            GL.DetachShader(programIndex, vertexShaderIndex);
            GL.DetachShader(programIndex, fragmentShaderIndex);
        }

        private static void LinkShaders(int vertexShaderIndex, int fragmentShaderIndex, int programIndex)
        {
            GL.AttachShader(programIndex, vertexShaderIndex);
            GL.AttachShader(programIndex, fragmentShaderIndex);

            GL.LinkProgram(programIndex);
            GL.GetProgram(programIndex, GetProgramParameterName.LinkStatus, out int linkStatus);

            bool linkingFailed = linkStatus == (int)All.False;

            if (linkingFailed)
            {
                GL.DeleteProgram(programIndex);
                GL.DeleteShader(vertexShaderIndex);
                GL.DeleteShader(fragmentShaderIndex);
                throw new Exception("Shader program failed to link");
            }
        }

        private static void CreateShaderProgram(Shader shader, out int vert, out int frag, out int prog)
        {
            vert = GL.CreateShader(ShaderType.VertexShader);
            frag = GL.CreateShader(ShaderType.FragmentShader);
            prog = GL.CreateProgram();

            if (!ShaderCompiler.TryCompileShader(vert, shader.VertexShader))
            {
                GL.DeleteShader(vert);
                GL.DeleteShader(frag);
                GL.DeleteProgram(prog);
                throw new Exception("Vertex shader failed to compile");
            }

            if (!ShaderCompiler.TryCompileShader(frag, shader.FragmentShader))
            {
                GL.DeleteShader(vert);
                GL.DeleteShader(frag);
                GL.DeleteProgram(prog);
                throw new Exception("Fragment shader failed to compile");
            }
        }

        public int GetUniformLocation(string name)
        {
            if (uniformLocations.TryGetValue(name, out int loc))
                return loc;

            loc = GL.GetUniformLocation(ProgramHandle, name);

            GLUtilities.PrintGLErrors(true);

            //switch (GL.GetError())
            //{
            //    case ErrorCode.InvalidValue:
            //        Logger.Error($"{ProgramHandle} is not a value generated by OpenGL.");
            //        break;
            //    case ErrorCode.InvalidOperation:
            //        Logger.Error($"{ProgramHandle} is not a program object or has not been linked succesfully.");
            //        break;
            //}

            uniformLocations.Add(name, loc);
            return loc;
        }

        public TextureUnit GetTextureUnitForUniform(int uniformLocation)
        {
            if (textureUnitAssignments.TryGetValue(uniformLocation, out var index))
                return TypeConverter.Convert(index);

            index = textureUnitAssignments.Count;

            if (index > GLUtilities.GetMaximumAmountOfTextureUnits())
                Logger.Error($"Exceeded maximum texture unit count. Your material cannot have more than {GLUtilities.GetMaximumAmountOfTextureUnits()} textures.");

            textureUnitAssignments.Add(uniformLocation, index);

            return TypeConverter.Convert(index);
        }
    }
}
