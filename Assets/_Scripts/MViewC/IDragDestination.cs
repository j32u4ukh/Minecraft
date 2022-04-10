using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace udemy
{
    /// <summary>
    /// Components that implement this interfaces can act as the destination for
    /// dragging a `DragItem`.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IDragDestination<T> where T : class
    {
        /// <summary>
        /// How many of the given item can be accepted.
        /// 
        /// 原專案名稱為 MaxAcceptable
        /// </summary>
        /// <param name="item">The item type to potentially be added.</param>
        /// <returns>If there is no limit Int.MaxValue should be returned.</returns>
        int getCapacity(T item);

        /// <summary>
        /// Update the UI and any data to reflect adding the item to this destination.
        /// 
        /// 原專案名稱為 AddItems
        /// </summary>
        /// <param name="item">The item type.</param>
        /// <param name="number">The quantity of items.</param>
        void addItems(T item, int number);
    }
}