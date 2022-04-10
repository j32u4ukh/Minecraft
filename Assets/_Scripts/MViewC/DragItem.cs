using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace udemy
{
    /// <summary>
    /// Allows a UI element to be dragged and dropped from and to a container.
    /// 
    /// Create a subclass for the type you want to be draggable. 
    /// Then place on the UI element you want to make draggable.
    /// 
    /// During dragging, the item is reparented to the parent canvas.
    /// 
    /// After the item is dropped it will be automatically return to the original UI parent. 
    /// It is the job of components implementing `IDragContainer`, `IDragDestination and `IDragSource` 
    /// to update the interface after a drag has occurred.
    /// </summary>
    /// <typeparam name="T">The type that represents the item being dragged.</typeparam>
    public class DragItem<T> : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler where T : class
    {
        // PRIVATE STATE
        Vector3 start_position;
        Transform original_parent;
        IDragSource<T> source;

        Canvas canvas;
        CanvasGroup canvas_group;

        private void Start()
        {
            source = GetComponentInParent<IDragSource<T>>();
            canvas = GetComponentInParent<Canvas>();
            canvas_group = GetComponent<CanvasGroup>();
        }

        #region ��@ IBeginDragHandler
        // �}�l�즲������
        void IBeginDragHandler.OnBeginDrag(PointerEventData eventData)
        {
            start_position = transform.position;
            original_parent = transform.parent;

            // ���\ Raycast �~��ϥΩ즲�\��
            canvas_group.blocksRaycasts = false;

            // �] Canvas �@��������
            transform.SetParent(canvas.transform, true);
        }
        #endregion

        #region ��@ IDragHandler
        // �즲���~��
        void IDragHandler.OnDrag(PointerEventData eventData)
        {
            transform.position = eventData.position;
        }
        #endregion

        #region ��@ IEndDragHandler
        // �q�즲���A�U��}�ƹ�������
        void IEndDragHandler.OnEndDrag(PointerEventData eventData)
        {
            // �۹�������󪺦�m
            transform.position = start_position;

            // �קK UI �צ� Raycast
            canvas_group.blocksRaycasts = true;

            // �٭������
            transform.SetParent(original_parent, true);

            IDragDestination<T> container;

            // �Y pointer �S���b�C������W
            if (!EventSystem.current.IsPointerOverGameObject())
            {
                // �H canvas �@���Q�즲���~��������(�������b�������ͦ��۹����� 3D ���~)
                // PS: �í��u�N canvas �U�� 2D ���~�R���A�ӬO�b�e���N�N InventoryItem ���������٭즨�쥻�����
                container = canvas.GetComponent<IDragDestination<T>>();
            }

            // �Y pointer �b�C������W
            else
            {
                // ���ը��o�Ӫ��� IDragDestination �H��m�Q�즲�����~
                container = getContainer(eventData);
            }

            // �Y�즲�����I���� null�A�N�Q�즲�����~��J container
            if (container != null)
            {
                dropItemIntoContainer(container);
            }
        }

        /// <summary>
        /// �� pointer ���V������P���������j�M IDragDestination�A�ê�^�t�� IDragDestination ������C
        /// �ѩ�|�@�����W�h�M��A�̲ױN�|�M��� canvas �ê�^�F���D���V�������ݩ� UI�A�ӬO���V�C���Ŷ����a��C
        /// </summary>
        /// <param name="eventData"></param>
        /// <returns></returns>
        private IDragDestination<T> getContainer(PointerEventData eventData)
        {
            IDragDestination<T> container = null;

            if (eventData.pointerEnter)
            {
                // GetComponentInParent: �|�u���P�_����ۨ��O�_���ؼвե�A�Y��������^�Ӳե�A���M��������F
                // �Y����ۨ��S���ؼвե�A�M��������A���Ӥ����󶶧Ǭd��]��p�G���P�_�W�@�h������A�Y�S�������ؼвե�A�A�M���W�W�@�h������(�ؼЪ��鯪������)�A�H�����k�d��^
                container = eventData.pointerEnter.GetComponentInParent<IDragDestination<T>>();
            }

            return container;
        }

        /// <summary>
        /// ���~���ʨ�ؼ����A�λP�ؼ���쪺���~�i��洫
        /// </summary>
        /// <param name="destination"></param>
        private void dropItemIntoContainer(IDragDestination<T> destination)
        {
            // �Y���ʪ� ���I �P �_�I �ۦP�A�h������^
            if (ReferenceEquals(source, destination))
            {
                return;
            }

            // �N���� IDragDestination �� destination �૬�� IDragContainer
            IDragContainer<T> destinationContainer = destination as IDragContainer<T>;

            // �N���� IDragSource �� source �૬�� IDragContainer
            IDragContainer<T> sourceContainer = source as IDragContainer<T>;

            // IDragContainer �P�ɥ]�t IDragSource �M IDragDestination�A�૬���i��|���Ѫ���]����H
            // attempt: ���աF�]�� attemptSimpleTransfer �M attemptSwap �����i��]�����󤣲ŦӨS������A���򳣤���

            // Swap won't be possible
            // �૬���ѡB�즲���ت��a���b�޲z�C���� �� ���ʪ� ���I �P �_�I �Ҧs�񪫫~�ۦP
            // �D�n�B�z�ؼ����Ū��A�άO�ӷ���쪺���~�P�ؼ���쪺���~�A��̬ۦP�����p�C
            if (destinationContainer == null ||
                sourceContainer == null ||
                destinationContainer.GetItem() == null ||
                ReferenceEquals(destinationContainer.GetItem(), sourceContainer.GetItem()))
            {
                attemptSimpleTransfer(destination);
                return;
            }

            // �B�z�ؼ���줣���šA�Ψӷ���쪺���~�P�ؼ���쪺���~�A��̤��ۦP�C�����쪺���~�洫�A�Τ������洫����Ӥ��򳣤���
            attemptSwap(destinationContainer, sourceContainer);
        }

        /// <summary>
        /// �Y���J�ؼ���즳�Ŧ�i�񪫫~�A�h�N���~���J�C�ھ���쪬�A�A�i��L�k���J�B�������J�Υ������J�C
        /// �D�n�B�z�ؼ����Ū��A�άO�ӷ���쪺���~�P�ؼ���쪺���~�A��̬ۦP�����p�C
        /// </summary>
        /// <param name="destination"></param>
        /// <returns></returns>
        private bool attemptSimpleTransfer(IDragDestination<T> destination)
        {
            Debug.Log($"[DragItem] attemptSimpleTransfer | destination: {destination}");

            // �Q�즲�����~
            T draggingItem = source.GetItem();

            // �Q�즲�����~���ƶq
            int draggingNumber = source.GetNumber();

            // �i��J��쪺���~�ƶq
            int acceptable = destination.MaxAcceptable(draggingItem);

            // �̦h�i��J��쪺���~�ƶq(���W�L��쥻��������)
            int toTransfer = Mathf.Min(acceptable, draggingNumber);

            // �i���J�ؼ����
            if (toTransfer > 0)
            {
                // �ӷ���쪺���~�ƶq��� toTransfer ��
                source.RemoveItems(toTransfer);

                // �ؼ���쪺���~�ƶq�W�[ toTransfer ��
                destination.AddItems(draggingItem, toTransfer);

                Debug.Log($"[DragItem] attemptSimpleTransfer | source: {source}, toTransfer: {toTransfer}");

                return false;
            }

            return true;
        }

        /// <summary>
        /// �洫����쪺���~�C
        /// �B�z�ؼ���줣���šA�Ψӷ���쪺���~�P�ؼ���쪺���~�A��̤��ۦP�C�����쪺���~�洫�A�Τ������洫����Ӥ��򳣤���
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="source"></param>
        private void attemptSwap(IDragContainer<T> destination, IDragContainer<T> source)
        {
            Debug.Log($"[DragItem] attemptSimpleTransfer | destination: {destination}, source: {source}");

            #region �ȮɱN���~�q���䳣�����CProvisionally remove item from both sides.
            T source_item = source.GetItem();
            int n_source = source.GetNumber();

            T destination_item = destination.GetItem();
            int n_destination = destination.GetNumber();

            source.RemoveItems(n_source);
            destination.RemoveItems(n_destination);
            #endregion

            // �p��W�X destination ���e�\�ƶq���ӼơA�N�|�Q��^ source ���
            var n_back_to_source = calculateTakeBack(source_item, n_source, source, destination);

            // �p��W�X source ���e�\�ƶq���ӼơA�N�|�Q��^ destination ���
            var n_back_to_destination = calculateTakeBack(destination_item, n_destination, destination, source);

            // �������ƶq�����~�ݭn�Q��^ source ���
            if (n_back_to_source > 0)
            {
                source.AddItems(source_item, n_back_to_source);
                n_source -= n_back_to_source;
            }

            // �������ƶq�����~�ݭn�Q��^ destination ���
            if (n_back_to_destination > 0)
            {
                destination.AddItems(destination_item, n_back_to_destination);
                n_destination -= n_back_to_destination;
            }

            // �Y�䤤�@����쪺�e�\�ƶq�����H�������~�����J�A�h�פ�洫��쪺�y�{
            if (source.MaxAcceptable(destination_item) < n_destination ||
                destination.MaxAcceptable(source_item) < n_source)
            {
                // �N�Ѿl�ƶq�[�^�쥻�����
                destination.AddItems(destination_item, n_destination);
                source.AddItems(source_item, n_source);

                return;
            }

            #region �N�Ѿl�ƶq�[�J�U�۪��ؼ���줤
            if (n_destination > 0)
            {
                source.AddItems(destination_item, n_destination);
            }

            if (n_source > 0)
            {
                destination.AddItems(source_item, n_source);
            } 
            #endregion
        }

        /// <summary>
        /// �p��W�X�ؼ����e�\�ƶq���ӼơA�N�|�Q��^�쥻�����
        /// </summary>
        /// <param name="item"></param>
        /// <param name="n_moved"></param>
        /// <param name="source"></param>
        /// <param name="destination"></param>
        /// <returns></returns>
        private int calculateTakeBack(T item, int n_moved, IDragContainer<T> source, IDragContainer<T> destination)
        {
            var takeBackNumber = 0;

            // �ؼ����i���J�ƶq
            var destinationMaxAcceptable = destination.MaxAcceptable(item);

            // �Y �i���J�ƶq �֩� �n���J�ƶq
            if (destinationMaxAcceptable < n_moved)
            {
                // �� takeBackNumber �Ӫ��~�L�k���J�A�N���^�쥻�����
                takeBackNumber = n_moved - destinationMaxAcceptable;

                // �ӷ���쪺�i���J�ƶq
                var sourceTakeBackAcceptable = source.MaxAcceptable(item);

                // Abort and reset
                // �ӷ���쪺�i���J�ƶq �֩� ���^�쥻����쪺�ƶq(�q����첾�X�A���^�h�A�ƶq�N�W�X����H)
                if (sourceTakeBackAcceptable < takeBackNumber)
                {
                    return 0;
                }
            }

            return takeBackNumber;
        }
        #endregion
    }
}