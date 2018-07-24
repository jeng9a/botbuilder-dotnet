// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Bot.Builder
{
    /// <summary>
    /// Implements IPropertyAccessor for an IPropertyContainer.
    /// </summary>
    /// <typeparam name="T">type of value the propertyAccessor accesses.</typeparam>
    public class BotStatePropertyAccessor<T>
    {
        private BotState _botState;
        private Func<T> _defaultValueFactory;

        internal BotStatePropertyAccessor(BotState botState, string name, Func<T> defaultValueFactory)
        {
            _botState = botState;
            Name = name;
            if (defaultValueFactory == null)
            {
                _defaultValueFactory = () => default(T);
            }
            else
            {
                _defaultValueFactory = defaultValueFactory;
            }
        }

        /// <summary>
        /// Gets name of the property.
        /// </summary>
        /// <value>
        /// name of the property.
        /// </value>
        public string Name { get; private set; }

        /// <summary>
        /// Delete the property.
        /// </summary>
        /// <param name="turnContext">turn context</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public Task DeleteAsync(ITurnContext turnContext)
        {
            return _botState.DeletePropertyValueAsync(turnContext, Name);
        }

        /// <summary>
        /// Get the property value.
        /// </summary>
        /// <param name="turnContext">turn context</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<T> GetAsync(ITurnContext turnContext)
        {
            try
            {
                return await _botState.GetPropertyValueAsync<T>(turnContext, Name).ConfigureAwait(false);
            }
            catch (KeyNotFoundException)
            {
                // ask for default value from factory
                var result = _defaultValueFactory();

                // save default value for any further calls
                await SetAsync(turnContext, result).ConfigureAwait(false);
                return result;
            }
        }

        /// <summary>
        /// Set the property value.
        /// </summary>
        /// <param name="turnContext">turn context.</param>
        /// <param name="value">value.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public Task SetAsync(ITurnContext turnContext, T value)
        {
            return _botState.SetPropertyValueAsync(turnContext, Name, value);
        }
    }
}
