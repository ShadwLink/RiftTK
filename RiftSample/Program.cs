using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Input;
using OpenTK.Platform;


namespace RiftSample
{
    class Program
    {
        [STAThread]
        public static void Main()
        {
            using (RiftSample example = new RiftSample())
            {
                //Utilities.set.SetWindowTitle(example);
                example.Run(60.0, 0.0);
            }
        }
    }
}
