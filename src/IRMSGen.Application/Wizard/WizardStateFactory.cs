using IRMSGen.Contracts.Api;
using IRMSGen.Contracts.DataSources;
using IRMSGen.Contracts.Dtos;
using IRMSGen.Contracts.Errors;
using IRMSGen.Contracts.ExternalServices;
using IRMSGen.Contracts.Wizard;

namespace IRMSGen.Application.Wizard;

public static class WizardStateFactory
{
    public static WizardState CreateDefault()
    {
        var state = new WizardState();

        var mainDbCredential = new ConnectorCredentialDefinition
        {
            Id = "main-postgres",
            Name = "MainDb",
            ConnectorType = "PostgreSQL",
            Host = "127.0.0.1",
            Port = 5432,
            Database = "orders",
            Username = "postgres",
            Password = string.Empty,
            SecretReference = "local:postgres-password",
            Description = "اتصال پیش‌فرض PostgreSQL برای سرویس"
        };

        state.ConnectorCredentials.Add(mainDbCredential);
        state.DatabaseConnection.CredentialId = mainDbCredential.Id;
        state.DatabaseConnection.Host = "127.0.0.1";
        state.DatabaseConnection.Port = 5432;
        state.DatabaseConnection.Database = "orders";
        state.DatabaseConnection.Username = "postgres";
        state.DatabaseConnection.Password = string.Empty;
        state.DatabaseConnection.SecretReference = "local:postgres-password";

        state.DataObjects.Add(new DataObjectDefinition
        {
            Kind = "Table",
            Schema = "sales",
            DatabaseName = "orders",
            EntityName = "Order",
            Fields =
            [
                new() { SourceName = "id", PropertyName = "Id", DbType = "uuid", ClrType = "Guid", IsPrimaryKey = true },
                new() { SourceName = "customer_name", PropertyName = "CustomerName", DbType = "varchar", ClrType = "string" },
                new() { SourceName = "total_amount", PropertyName = "TotalAmount", DbType = "numeric", ClrType = "decimal" },
                new() { SourceName = "created_at", PropertyName = "CreatedAt", DbType = "timestamp", ClrType = "DateTime" }
            ]
        });

        state.Dtos.Add(new DtoDefinition
        {
            Name = "CreateOrderRequest",
            EntityName = "Order",
            Kind = "Request",
            Fields =
            [
                new() { SourceField = "CustomerName", Name = "CustomerName", Type = "string", Required = true },
                new() { SourceField = "TotalAmount", Name = "TotalAmount", Type = "decimal", Required = true }
            ]
        });
        state.Dtos.Add(new DtoDefinition
        {
            Name = "OrderResponse",
            EntityName = "Order",
            Kind = "Response",
            Fields =
            [
                new() { SourceField = "Id", Name = "Id", Type = "Guid", Required = true },
                new() { SourceField = "CustomerName", Name = "CustomerName", Type = "string", Required = true },
                new() { SourceField = "TotalAmount", Name = "TotalAmount", Type = "decimal", Required = true },
                new() { SourceField = "CreatedAt", Name = "CreatedAt", Type = "DateTime", Required = true }
            ]
        });

        state.ApiEndpoints.Add(new ApiEndpointDefinition
        {
            Name = "CreateOrder",
            Feature = "Orders",
            Method = "POST",
            Route = "/api/orders",
            OperationType = "Command",
            RequestDto = "CreateOrderRequest",
            ResponseDto = "OrderResponse"
        });
        state.ApiEndpoints.Add(new ApiEndpointDefinition
        {
            Name = "SearchOrders",
            Feature = "Orders",
            Method = "GET",
            Route = "/api/orders",
            OperationType = "Query",
            ResponseDto = "OrderResponse",
            UsesPaging = true
        });

        state.Errors.Add(new ErrorDefinition
        {
            EndpointName = "CreateOrder",
            Code = "ORDER_CUSTOMER_REQUIRED",
            HttpStatus = 400,
            Type = "Validation",
            Message = "Customer name is required."
        });
        state.Errors.Add(new ErrorDefinition
        {
            EndpointName = "CreateOrder",
            Code = "ORDER_DUPLICATE",
            HttpStatus = 409,
            Type = "Business",
            Message = "Order already exists."
        });

        state.ExternalServices.Add(new ExternalServiceDefinition
        {
            Name = "CustomerProfileService",
            Type = "REST",
            BaseUrl = "https://customer-api.company.com",
            Authentication = "Bearer Token",
            Operations =
            [
                new()
                {
                    Name = "GetCustomerProfile",
                    Method = "GET",
                    Path = "/customers/{customerId}/profile",
                    RequestModel = "GetCustomerProfileRequest",
                    ResponseModel = "CustomerProfileResponse",
                    ErrorMappings =
                    [
                        new() { ExternalStatus = 404, ExternalCode = "CUSTOMER_NOT_FOUND", InternalStatus = 404, InternalCode = "CUSTOMER_PROFILE_NOT_FOUND" },
                        new() { ExternalStatus = 503, ExternalCode = "SERVICE_UNAVAILABLE", InternalStatus = 503, InternalCode = "CUSTOMER_SERVICE_UNAVAILABLE" }
                    ]
                }
            ]
        });

        state.Deployment.Registry = "registry.company.com";
        state.Deployment.ImageName = "orderservice";
        state.Deployment.KubernetesNamespace = "orders";

        return state;
    }
}
