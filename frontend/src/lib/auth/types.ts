/** Mirrors `Inventory.Application.Auth.AccountProfile` exactly - the full response
 * shape of `GET /api/v1/account/me` (task 3.12). */
export type RoleName = 'Owner' | 'Admin' | 'WarehouseStaff' | 'RestaurantSupervisor' | 'User';

export interface AccountProfile {
  userId: string;
  companyId: string;
  fullName: string;
  mobileNumber: string;
  role: RoleName | null;
  warehouseScopeIds: string[];
  restaurantScopeIds: string[];
  permissionCodes: string[];
}
