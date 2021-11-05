﻿using System;
using System.Collections.Generic;
using System.Linq;
using OzonEdu.MerchandiseService.Domain.AggregatesModel.EmployeeAggregate;
using OzonEdu.MerchandiseService.Domain.Events.MerchOrderAggregate;
using OzonEdu.MerchandiseService.Domain.Exceptions;
using OzonEdu.MerchandiseService.Domain.SeedWork;

namespace OzonEdu.MerchandiseService.Domain.AggregatesModel.MerchOrderAggregate
{
    public sealed class MerchOrder : Entity, IAggregateRoot
    {
        public Employee Receiver { get; }
        public Employee Manager { get; private set; }
        public OrderStatus Status { get; private set; }
        public DateTime StatusDateTime { get; private set; }
        public string StatusDescription { get; private set; }
        
        private readonly List<OrderItem> _orderItems;
        public IReadOnlyCollection<OrderItem> OrderItems => _orderItems.AsReadOnly();
        
        private MerchOrder()
        {
            _orderItems = new List<OrderItem>();
            Status = OrderStatus.Draft;
        }

        public MerchOrder(Employee receiver) : this()
        {
            Receiver = receiver ?? throw new ArgumentNullException(nameof(receiver));
        }

        public void AddOrderItem(long sku, string skuDescription, int quantity, DateTime utcNow)
        {
            if (!Status.Equals(OrderStatus.Draft) && !Status.Equals(OrderStatus.Created))
            {
                ThrowNotAllowedToAddOrderItemException();
            }

            var orderItem = OrderItem.Create(sku, skuDescription, quantity, utcNow);
            _orderItems.Add(orderItem);
            
            if(Status.Equals(OrderStatus.Draft)) Status = OrderStatus.Created;
        }

        public void AssignTo(Employee manager, DateTime utcNow)
        {
            if (manager == null) throw new ArgumentNullException(nameof(manager));

            bool reassign = (Manager != null);

            Manager = manager;

            if (reassign) AddDomainEvent(new OrderReassignedDomainEvent(this));
            else SetAssignedStatus(utcNow);
        }

        public void SetAssignedStatus(DateTime utcNow)
        {
            if (Manager == null)
            {
                ThrowStatusChangeToAssignedWithoutManagerException();
            }

            ValidateAndSetStatusTo(OrderStatus.Assigned,
                $"Assigned to manager {Manager.PersonName} ({Manager.Email.Value}).", utcNow);
        }

        public void SetInProgressStatus(DateTime utcNow) =>
            ValidateAndSetStatusTo(OrderStatus.InProgress, OrderStatus.InProgress.DefaultDescription, utcNow);

        public void SetDeferredStatus(DateTime utcNow) =>
            ValidateAndSetStatusTo(OrderStatus.Deferred, OrderStatus.Deferred.DefaultDescription, utcNow);

        public void SetReservedStatus(DateTime utcNow) =>
            ValidateAndSetStatusTo(OrderStatus.Reserved, OrderStatus.Reserved.DefaultDescription, utcNow);

        public void SetCompletedStatus(DateTime utcNow) =>
            ValidateAndSetStatusTo(OrderStatus.Completed, OrderStatus.Completed.DefaultDescription, utcNow);

        public void SetCanceledStatus(string reason, DateTime utcNow) =>
            ValidateAndSetStatusTo(OrderStatus.Canceled, reason, utcNow);

        private void ValidateAndSetStatusTo(OrderStatus newStatus, string reason, DateTime utcNow)
        {
            if (IsValidStatusChangeTo(newStatus.Id))
                ChangeStatus(newStatus, reason, utcNow);
            else
                ThrowStatusChangeException(newStatus);
        }

        private Dictionary<int, IEnumerable<int>> _allowedStatusTransitions =
            new()
            {
                [OrderStatus.Draft.Id] = new List<int>() {OrderStatus.Created.Id, OrderStatus.Canceled.Id},
                [OrderStatus.Created.Id] = new List<int>() {OrderStatus.Assigned.Id, OrderStatus.Canceled.Id},
                [OrderStatus.Assigned.Id] = new List<int>() {OrderStatus.InProgress.Id, OrderStatus.Canceled.Id},
                [OrderStatus.InProgress.Id] = new List<int>()
                    {OrderStatus.Deferred.Id, OrderStatus.Reserved.Id, OrderStatus.Canceled.Id},
                [OrderStatus.Deferred.Id] = new List<int>() {OrderStatus.InProgress.Id, OrderStatus.Canceled.Id},
                [OrderStatus.Reserved.Id] = new List<int>() {OrderStatus.Completed.Id, OrderStatus.Canceled.Id},
                [OrderStatus.Completed.Id] = new List<int>() { },
                [OrderStatus.Canceled.Id] = new List<int>() { }
            };

        private bool IsValidStatusChangeTo(int changeToStatusId)
        {
            return _allowedStatusTransitions.ContainsKey(Status.Id) &&
                   _allowedStatusTransitions[Status.Id].Contains(changeToStatusId);
        }

        private void ChangeStatus(OrderStatus newStatus, string description, DateTime utcNow)
        {
            StatusDescription = description;
            Status = newStatus;
            StatusDateTime = utcNow;

            AddDomainEvent(new OrderChangedStatusDomainEvent(this));
        }

        private void ThrowStatusChangeException(OrderStatus statusChangeTo)
        {
            throw new MerchOrderAggregateException(
                $"Not possible to change the order status from {Status} to {statusChangeTo}.");
        }

        private void ThrowStatusChangeToAssignedWithoutManagerException()
        {
            throw new MerchOrderAggregateException(
                $"Not possible to change the order status to {OrderStatus.Assigned} when {nameof(Manager)} is null");
        }

        public void ThrowNotAllowedToAddOrderItemException()
        {
            throw new MerchOrderAggregateException(
                $"Not allowed to add items to order with status {Status}");
        }
    }
}