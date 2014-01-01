using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using System.Diagnostics;
using System.IO;
using System.Drawing;
using RiftSharp;

namespace RiftSample
{
    public class RiftSample : GameWindow
    {
        public enum Eye
        {
            Left, Right
        };

        protected int shader = 0;
        protected int vertShader = 0;
        protected int fragShader = 0;

        protected int colorTextureID;
        protected int framebufferID;
        protected int depthRenderBufferID;

        private int LensCenterLocation;
        private int ScreenCenterLocation;
        private int ScaleLocation;
        private int ScaleInLocation;
        private int HmdWarpParamLocation;

        private Rectangle viewPortLeft;
        private Rectangle viewPortRight;
        private Matrix4 projLeft;
        private Matrix4 projRight;
        private Matrix4 viewLeft;
        private Matrix4 viewRight;

        private float rotation = 0.0f;

        private float distance = -10.0f;
        private float projectionCenterOffset;
        private float IOD = 0.4399979f;

        public RiftSample() : base(640, 400)
        {
            //this.WindowState = WindowState.Fullscreen;
            init();
            distortionCorrection(ClientSize.Width, ClientSize.Height);
        }

        private void init()
        {
            viewPortLeft = new Rectangle(0, 0, Width / 2, Height);
            viewPortRight = new Rectangle(Width / 2, 0, Width / 2, Height);

            float aspectRatio = 1280 / (2 * (float)800);
            float halfScreenDistance = (0.0935999975f / 2);
            float yfov = (float)2.0f * (float)Math.Atan(halfScreenDistance / 0.0410000011f);
            float viewCenter = 0.149759993f * 0.25f;
            float eyeProjectionShift = viewCenter - 0.0635000020f * 0.5f;
            projectionCenterOffset = 4.0f * eyeProjectionShift / 0.149759993f;
            var projCenter = Matrix4.CreatePerspectiveFieldOfView(yfov, aspectRatio, 0.3f, 1000.0f);
            projLeft = Matrix4.CreateTranslation(projectionCenterOffset, 0, 0) * projCenter;
            projRight = Matrix4.CreateTranslation(-projectionCenterOffset, 0, 0) * projCenter;
            float halfIPD = 0.0640000030f * 0.5f;
            viewLeft = Matrix4.CreateTranslation(halfIPD * viewCenter, 0, 0);// *viewCenter;
            viewRight = Matrix4.CreateTranslation(-halfIPD * viewCenter, 0, 0);// *viewCenter;
        }

        private void distortionCorrection(int screenWidth, int screenHeight)
        {
            initShaders("distortion_vs.glsl", "distortion_fs.glsl");
            initFBO(screenWidth, screenHeight);

            LensCenterLocation = GL.GetUniformLocation(shader, "LensCenter");
            ScreenCenterLocation = GL.GetUniformLocation(shader, "ScreenCenter");
            ScaleLocation = GL.GetUniformLocation(shader, "Scale");
            ScaleInLocation = GL.GetUniformLocation(shader, "ScaleIn");
            HmdWarpParamLocation = GL.GetUniformLocation(shader, "HmdWarpParam");
        }

        protected void initShaders(String vertexShader, String fragmentShader)
        {
            Debug.WriteLine("Init shaders");
            shader = GL.CreateProgram();

            loadShader(vertexShader, ShaderType.VertexShader, shader, out vertShader);
            loadShader(fragmentShader, ShaderType.FragmentShader, shader, out fragShader);

            if (vertShader != 0 && fragShader != 0)
            {
                GL.AttachShader(shader, vertShader);
                GL.AttachShader(shader, fragShader);

                int[] temp = new int[1];
                GL.LinkProgram(shader);
                GL.GetProgram(shader, ProgramParameter.LinkStatus, out temp[0]);
                if (temp[0] != 1)
                {
                    Debug.Print("Linkage error");
                    //System.exit(0);
                }

                GL.ValidateProgram(shader);
                GL.GetProgram(shader, ProgramParameter.LinkStatus, out temp[0]);
                if (temp[0] != 1)
                {
                    Debug.Print("Linkage error");
                    //System.exit(0);
                }
            }
            else
            {
                Debug.WriteLine("No shaders");
                //System.exit(0);
            }
        }

        private void loadShader(String shaderFileName, ShaderType shaderType, int program, out int address)
        {
            address = GL.CreateShader(shaderType);
            using (StreamReader sr = new StreamReader("shaders/" + shaderFileName))
            {
                GL.ShaderSource(address, sr.ReadToEnd());
            }
            GL.CompileShader(address);
            //GL.AttachShader(program, address);
            Console.WriteLine(GL.GetShaderInfoLog(address));
        }

        private void initFBO(int screenWidth, int screenHeight)
        {
            Debug.WriteLine("InitFBO");
            GL.GenFramebuffers(1, out framebufferID);
            GL.GenTextures(1, out colorTextureID);
            GL.GenRenderbuffers(1, out depthRenderBufferID);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, framebufferID);

            // initialize color texture
            GL.BindTexture(TextureTarget.Texture2D, colorTextureID);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMagFilter.Linear);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, screenWidth, screenHeight, 0, PixelFormat.Rgba, PixelType.Int, (System.IntPtr)null);//(java.nio.ByteBuffer)null);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Clamp);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Clamp);

            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, colorTextureID, 0);

            // initialize depth renderbuffer
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, depthRenderBufferID);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.DepthComponent24, screenWidth, screenHeight);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, depthRenderBufferID);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            //SetModelviewMatrix(Matrix4.CreateRotationY((float)e.Time) * modelviewMatrix);

            if (Keyboard[OpenTK.Input.Key.Escape])
                Exit();

            // IPD
            if (Keyboard[OpenTK.Input.Key.PageUp])
            {
                IOD += 0.001f;
                Debug.WriteLine("IOD: " + IOD);
            }
            if (Keyboard[OpenTK.Input.Key.PageDown])
            {
                IOD -= 0.001f;
                Debug.WriteLine("IOD: " + IOD);
            }

            // Distance
            if (Keyboard[OpenTK.Input.Key.Up])
            {
                distance += 0.1f;
            }
            if (Keyboard[OpenTK.Input.Key.Down])
            {
                distance -= 0.1f;
            }

        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            beginOffScreenRenderPass();

            GL.ClearColor(Color.CornflowerBlue);
            GL.ClearDepth(1.0f);                   // Set background depth to farthest
            GL.Enable(EnableCap.DepthTest);   // Enable depth testing for z-culling
            GL.DepthFunc(DepthFunction.Lequal);    // Set the type of depth-test
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            //GL.MatrixMode(MatrixMode.Projection);
            //GL.LoadMatrix(ref projection); 

            drawScene(viewPortLeft, projLeft, true);
            drawScene(viewPortRight, projRight, false);
            //GL.LoadIdentity();

            GL.Viewport(0, 0, Width, Height);
            //GL.MatrixMode(MatrixMode.Modelview);
            //GL.LoadIdentity();

            //endOffScreenRenderPass();
            renderToScreen();

            GL.Flush();

            SwapBuffers();
        }

        private void drawScene(Rectangle viewPort, Matrix4 projection, bool isLeft)
        {
            GL.Viewport(viewPort);

            if (isLeft)
            {
                //GL.MatrixMode(MatrixMode.Projection);
                //GL.LoadIdentity();
                //GL.Translate(projectionCenterOffset, 0.0f, 0.0f);

                //Matrix4 perspective = Matrix4.Perspective(90.0f, (Width/2.0f) / Height, 0.1f, 500.0f);//(float)Math.PI / 4, Width / Height, 0.001f, 500.0f);
                //GL.MultMatrix(ref perspective);

                //GL.MatrixMode(MatrixMode.Modelview);
                //GL.LoadIdentity();
                //GL.Translate(IOD / 2, 0.0f, 0.0f);

                GL.MatrixMode(MatrixMode.Projection);
                GL.LoadIdentity();
                //GL.Rotate(Hmd.Instance.YawPitchRoll.Yaw * 90.0f, 0, 1.0f, 0);
                GL.Translate(IOD / 2, 0, 0);

                //gluPerspective(90.0f, Width/ 2.0 / Height, 0.01f, 1000.0f);
                Matrix4 perspective = Matrix4.Perspective(90.0f, (Width / 2.0f) / Height, 0.1f, 500.0f);
                GL.MultMatrix(ref perspective);

                GL.MatrixMode(MatrixMode.Modelview);
                GL.LoadIdentity();
            }
            else
            {
                //GL.MatrixMode(MatrixMode.Projection);
                //GL.LoadIdentity();
                //GL.Translate(-projectionCenterOffset, 0.0f, 0.0f);

                //Matrix4 perspective = Matrix4.Perspective(90.0f, (Width / 2.0f) / Height, 0.1f, 500.0f);//Matrix4.CreatePerspectiveFieldOfView((float)Math.PI / 4, Width / Height, 0.001f, 500.0f);
                //GL.MultMatrix(ref perspective);

                //GL.MatrixMode(MatrixMode.Modelview);
                //GL.LoadIdentity();
                //GL.Translate(-IOD / 2, 0.0f, 0.0f);

                GL.MatrixMode(MatrixMode.Projection);
                GL.LoadIdentity();
                GL.Translate(-IOD / 2, 0, 0);

                //gluPerspective(90.0f, Width/ 2.0 / Height, 0.01f, 1000.0f);
                Matrix4 perspective = Matrix4.Perspective(90.0f, (Width / 2.0f) / Height, 0.1f, 500.0f);
                GL.MultMatrix(ref perspective);

                GL.MatrixMode(MatrixMode.Modelview);
                GL.LoadIdentity();
            }

            GL.PushMatrix();
            GL.Translate(0.0f, -1.0f, distance);
            GL.Rotate(rotation, 0.0f, 1.0f, 0.0f);
            drawCube();

            GL.PopMatrix();
            GL.Translate(0.0f, 1.2f, distance);
            GL.Rotate(rotation, 0.0f, 1.0f, 0.0f);
            drawTriangle();

            rotation += 0.5f;
            
            GL.LoadIdentity();
        }

        private void drawCube()
        {
            GL.Begin(BeginMode.Quads);                // Begin drawing the color cube with 6 quads
            // Top face (y = 1.0f)
            // Define vertices in counter-clockwise (CCW) order with normal pointing out
            GL.Color3(0.0f, 1.0f, 0.0f);     // Green
            GL.Vertex3(1.0f, 1.0f, -1.0f);
            GL.Vertex3(-1.0f, 1.0f, -1.0f);
            GL.Vertex3(-1.0f, 1.0f, 1.0f);
            GL.Vertex3(1.0f, 1.0f, 1.0f);

            // Bottom face (y = -1.0f)
            GL.Color3(1.0f, 0.5f, 0.0f);     // Orange
            GL.Vertex3(1.0f, -1.0f, 1.0f);
            GL.Vertex3(-1.0f, -1.0f, 1.0f);
            GL.Vertex3(-1.0f, -1.0f, -1.0f);
            GL.Vertex3(1.0f, -1.0f, -1.0f);

            // Front face  (z = 1.0f)
            GL.Color3(1.0f, 0.0f, 0.0f);     // Red
            GL.Vertex3(1.0f, 1.0f, 1.0f);
            GL.Vertex3(-1.0f, 1.0f, 1.0f);
            GL.Vertex3(-1.0f, -1.0f, 1.0f);
            GL.Vertex3(1.0f, -1.0f, 1.0f);

            // Back face (z = -1.0f)
            GL.Color3(1.0f, 1.0f, 0.0f);     // Yellow
            GL.Vertex3(1.0f, -1.0f, -1.0f);
            GL.Vertex3(-1.0f, -1.0f, -1.0f);
            GL.Vertex3(-1.0f, 1.0f, -1.0f);
            GL.Vertex3(1.0f, 1.0f, -1.0f);

            // Left face (x = -1.0f)
            GL.Color3(0.0f, 0.0f, 1.0f);     // Blue
            GL.Vertex3(-1.0f, 1.0f, 1.0f);
            GL.Vertex3(-1.0f, 1.0f, -1.0f);
            GL.Vertex3(-1.0f, -1.0f, -1.0f);
            GL.Vertex3(-1.0f, -1.0f, 1.0f);

            // Right face (x = 1.0f)
            GL.Color3(1.0f, 0.0f, 1.0f);     // Magenta
            GL.Vertex3(1.0f, 1.0f, -1.0f);
            GL.Vertex3(1.0f, 1.0f, 1.0f);
            GL.Vertex3(1.0f, -1.0f, 1.0f);
            GL.Vertex3(1.0f, -1.0f, -1.0f);
            GL.End();  // End of drawing color-cube
        }

        private void drawTriangle()
        {
            GL.Begin(BeginMode.Triangles);           // Begin drawing the pyramid with 4 triangles
            // Front
            GL.Color3(1.0f, 0.0f, 0.0f);     // Red
            GL.Vertex3(0.0f, 1.0f, 0.0f);
            GL.Color3(0.0f, 1.0f, 0.0f);     // Green
            GL.Vertex3(-1.0f, -1.0f, 1.0f);
            GL.Color3(0.0f, 0.0f, 1.0f);     // Blue
            GL.Vertex3(1.0f, -1.0f, 1.0f);

            // Right
            GL.Color3(1.0f, 0.0f, 0.0f);     // Red
            GL.Vertex3(0.0f, 1.0f, 0.0f);
            GL.Color3(0.0f, 0.0f, 1.0f);     // Blue
            GL.Vertex3(1.0f, -1.0f, 1.0f);
            GL.Color3(0.0f, 1.0f, 0.0f);     // Green
            GL.Vertex3(1.0f, -1.0f, -1.0f);

            // Back
            GL.Color3(1.0f, 0.0f, 0.0f);     // Red
            GL.Vertex3(0.0f, 1.0f, 0.0f);
            GL.Color3(0.0f, 1.0f, 0.0f);     // Green
            GL.Vertex3(1.0f, -1.0f, -1.0f);
            GL.Color3(0.0f, 0.0f, 1.0f);     // Blue
            GL.Vertex3(-1.0f, -1.0f, -1.0f);

            // Left
            GL.Color3(1.0f, 0.0f, 0.0f);       // Red
            GL.Vertex3(0.0f, 1.0f, 0.0f);
            GL.Color3(0.0f, 0.0f, 1.0f);       // Blue
            GL.Vertex3(-1.0f, -1.0f, -1.0f);
            GL.Color3(0.0f, 1.0f, 0.0f);       // Green
            GL.Vertex3(-1.0f, -1.0f, 1.0f);
            GL.End();   // Done drawing the pyramid
        }

        protected override void OnResize(EventArgs e)
        {
            Debug.WriteLine("Resize");
            //distortionCorrection(ClientSize.Width, ClientSize.Height);
            //    float widthToHeight = ClientSize.Width / (float)ClientSize.Height;
            //    SetProjectionMatrix(Matrix4.CreatePerspectiveFieldOfView(1.3f, widthToHeight, 1, 20));
        }



        /********************** JAVA SAMPLE *************************************/


        public void beginOffScreenRenderPass()
        {
            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, framebufferID);
        }

        public void endOffScreenRenderPass()
        {

        }

        public void renderToScreen()
        {
            GL.UseProgram(shader);

            GL.Enable(EnableCap.Texture2D);
            GL.Disable(EnableCap.DepthTest);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadIdentity();
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();
            GL.Disable(EnableCap.DepthTest);

            GL.ClearColor(Color.Green);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            GL.BindTexture(TextureTarget.Texture2D, colorTextureID);

            renderDistortedEye(Eye.Left, 0.0f, 0.0f, 0.5f, 1.0f);
            renderDistortedEye(Eye.Right, 0.5f, 0.0f, 0.5f, 1.0f);

            GL.UseProgram(0);
            GL.Enable(EnableCap.DepthTest);
        }

        public static float K0 = 1.0f;
        public static float K1 = 0.22f;
        public static float K2 = 0.24f;
        public static float K3 = 0.0f;

        public void renderDistortedEye(Eye eye, float x, float y, float width, float height)
        {
            float aspectRatio = width / height;

            float scaleFactor = 1.0f;

            float DistortionXCenterOffset;
            if (eye == Eye.Left)
            {
                DistortionXCenterOffset = 0.25f;
            }
            else
            {
                DistortionXCenterOffset = -0.25f;
            }

            //GL.Uniform2(LensCenterLocation, x + (width + DistortionXCenterOffset * 0.5f) * 0.5f, y + height * 0.5f);
            //GL.Uniform2(ScreenCenterLocation, x + width * 0.5f, y + height * 0.5f);
            //GL.Uniform2(ScaleLocation, (width / 2.0f) * scaleFactor, (height / 2.0f) * scaleFactor * aspectRatio); ;
            //GL.Uniform2(ScaleInLocation, (2.0f / width), (2.0f / height) / aspectRatio);
            GL.Uniform2(LensCenterLocation, x + (width + DistortionXCenterOffset * 0.5f) * 0.5f, y + height * 0.5f);
            GL.Uniform2(ScreenCenterLocation, x + width * 0.5f, y + height * 0.5f);
            GL.Uniform2(ScaleLocation, (width / 2.0f) * scaleFactor, (height / 2.0f) * scaleFactor * aspectRatio); ;
            GL.Uniform2(ScaleInLocation, (1.0f / width), (1.0f / height) / aspectRatio);

            GL.Uniform4(HmdWarpParamLocation, K0, K1, K2, K3);

            if (eye == Eye.Left)
            {
                GL.Begin(BeginMode.TriangleStrip);
                GL.TexCoord2(0.0f, 0.0f); GL.Vertex2(-1.0f, -1.0f);
                GL.TexCoord2(0.5f, 0.0f); GL.Vertex2(0.0f, -1.0f);
                GL.TexCoord2(0.0f, 1.0f); GL.Vertex2(-1.0f, 1.0f);
                GL.TexCoord2(0.5f, 1.0f); GL.Vertex2(0.0f, 1.0f);
                GL.End();
            }
            else
            {
                GL.Begin(BeginMode.TriangleStrip);
                GL.TexCoord2(0.5f, 0.0f); GL.Vertex2(0.0f, -1.0f);
                GL.TexCoord2(1.0f, 0.0f); GL.Vertex2(1.0f, -1.0f);
                GL.TexCoord2(0.5f, 1.0f); GL.Vertex2(0.0f, 1.0f);
                GL.TexCoord2(1.0f, 1.0f); GL.Vertex2(1.0f, 1.0f);
                GL.End();
            }
        }
    }
}
