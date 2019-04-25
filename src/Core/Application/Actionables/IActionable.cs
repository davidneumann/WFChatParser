using Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Application.Actionables
{
    public interface IActionable
    {
        bool IsRequestingControl { get; }

        Task TakeControl();

        void ShutDown();
    }
}
