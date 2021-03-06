﻿using System;
using System.Threading;
using System.Threading.Tasks;

using Sleeksoft.CB.Commands;

namespace Sleeksoft.CB.States
{
    // The circuit is closed. Therefore any call will be attempted.
    internal class StateClosed : ICircuitState
    {
        private readonly ICircuit m_Circuit;
        private readonly ICommand m_Command;
        private readonly int m_MaxFailuresBeforeTrip;

        private int m_FailureCount;

        public StateClosed(ICircuit circuit, TimeSpan commandTimeout, int maxFailuresBeforeTrip)
        {
            m_Circuit = circuit;
            m_Command = new Command(commandTimeout);
            m_MaxFailuresBeforeTrip = maxFailuresBeforeTrip;
        }

        public void Enter()
        {
            m_FailureCount = 0;
        }

        public bool IsOpen
        {
            get { return false; }
        }

        public bool IsHalfOpen
        {
            get { return false; }
        }

        public bool IsClosed
        {
            get { return true; }
        }

        // Execute synchronous command without result.
        public void ExecuteSync(Action command)
        {
            bool exceptionHappened = true;

            try
            {
                m_Command.ExecuteSync(command);
                exceptionHappened = false;
            }
            finally
            {
                if ( exceptionHappened )
                {
                    this.CommandFailed();
                }
                else
                {
                    this.CommandSucceeded();
                }
            }
        }

        // Execute synchronous command with result.
        public T ExecuteSync<T>(Func<T> command)
        {
            T result = default(T);
            bool exceptionHappened = true;

            try
            {
                result = m_Command.ExecuteSync(command);
                exceptionHappened = false;
            }
            finally
            {
                if ( exceptionHappened )
                {
                    this.CommandFailed();
                }
                else
                {
                    this.CommandSucceeded();
                }
            }

            return result;
        }

        // Execute synchronous command with result
        // Then execute fallback command if first command failed.
        // NB Only first command affects the circuit breaker.
        public T ExecuteSync<T>(Func<T> command, Func<T> fallbackCommand)
        {
            T result = default(T);

            try
            {
                result = m_Command.ExecuteSync(command);
                this.CommandSucceeded();
            }
            catch ( Exception )
            {
                this.CommandFailed();
                result = m_Command.ExecuteSync(fallbackCommand);
            }

            return result;
        }

        // Execute asynchronous command without result.
        public async Task ExecuteAsync(Func<Task> command)
        {
            bool exceptionHappened = true;

            try
            {
                await m_Command.ExecuteAsync(command);
                exceptionHappened = false;
            }
            finally
            {
                if ( exceptionHappened )
                {
                    this.CommandFailed();
                }
                else
                {
                    this.CommandSucceeded();
                }
            }
        }

        // Execute asynchronous command with result.
        public async Task<T> ExecuteAsync<T>(Func<Task<T>> command)
        {
            Task<T> task = default(Task<T>);
            bool exceptionHappened = true;

            try
            {
                task = m_Command.ExecuteAsync(command);
                await task;
                exceptionHappened = false;
            }
            finally
            {
                if ( exceptionHappened )
                {
                    this.CommandFailed();
                }
                else
                {
                    this.CommandSucceeded();
                }
            }

            return await task;
        }

        // Execute asynchronous command with result
        // Then execute fallback command if first command failed.
        // NB Only first command affects the circuit breaker.
        public async Task<T> ExecuteAsync<T>(Func<Task<T>> command, Func<Task<T>> fallbackCommand)
        {
            Task<T> task = default(Task<T>);

            try
            {
                task = m_Command.ExecuteAsync(command);
                await task;
                this.CommandSucceeded();
            }
            catch ( Exception )
            {
                this.CommandFailed();
                task = m_Command.ExecuteAsync(fallbackCommand);
                await task;
            }

            return await task;
        }

        private void CommandFailed()
        {
            if ( Interlocked.Increment(ref m_FailureCount) == m_MaxFailuresBeforeTrip )
            {
                m_Circuit.Open();
            }
        }

        private void CommandSucceeded()
        {
            m_FailureCount = 0;
        }
    }
}