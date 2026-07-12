namespace Plataforma.Domain.Enums;

/// <summary>
/// Papéis de acesso (RBAC — RF-AUTH-008). Guardado como int no banco.
/// Marco 1 usa um papel por usuário; a normalização para muitos-para-muitos
/// (tabelas roles/user_roles) fica para quando o painel admin exigir.
/// </summary>
public enum UserRole
{
    Customer = 0,
    Support = 1,
    Owner = 2
}
