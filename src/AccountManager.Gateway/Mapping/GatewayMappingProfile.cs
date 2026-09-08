using AccountManager.Gateway.Clients.Upstream;
using AccountManager.Gateway.Dtos.Responses;
using AutoMapper;

namespace AccountManager.Gateway.Mapping;

public sealed class GatewayMappingProfile : Profile
{
    public GatewayMappingProfile()
    {
        CreateMap<UpstreamTokenResponse, GatewayTokenResponse>();
        CreateMap<UpstreamMoneyResponse, GatewayMoneyResponse>();
        CreateMap<UpstreamBalanceResponse, GatewayBalanceResponse>();
        CreateMap<UpstreamTransactionItem, GatewayTransactionItem>();
        CreateMap<UpstreamTransactionListResponse, GatewayTransactionListResponse>();
    }
}
