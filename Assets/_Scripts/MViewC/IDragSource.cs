using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace udemy
{
    /// <summary>
    /// Components that implement this interfaces can act as the source for
    /// dragging a `DragItem`.
    /// </summary>
    /// <typeparam name="T">The type that represents the item being dragged.</typeparam>
    public interface IDragSource<T> where T : class
    {
        /// <summary>
        /// What item type currently resides in this source?
        /// 
        /// 原專案名稱為 GetItem
        /// </summary>
        T getItem();

        /// <summary>
        /// What is the quantity of items in this source?
        /// 
        /// 原專案名稱為 GetNumber
        /// </summary>
        int getNumber();

        /// <summary>
        /// Remove a given number of items from the source.
        /// 
        /// 原專案名稱為 RemoveItems
        /// </summary>
        /// <param name="number">
        /// This should never exceed the number returned by `GetNumber`.
        /// </param>
        void removeItems(int number);
    }
}