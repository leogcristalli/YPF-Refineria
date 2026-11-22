import React from 'react';
import { Outlet, Link, useLocation, useNavigate } from 'react-router-dom';
import {
    Truck, Scale, LayoutDashboard, Users, Package,
    Briefcase, UserCheck, Monitor,
    LogOut, ChevronRight, FileText, BarChart2, Database,
    Camera, MapPin, ListChecks, Shield, BookOpen
} from 'lucide-react';
import { useAuth } from '../auth/AuthContext';
import { rutaPermitida } from '../auth/puntosMenu';

const MainLayout = () => {
    const location = useLocation();
    const navigate = useNavigate();
    const { sesion, logout } = useAuth();

    const menuItems = [
        {
            group: "Operaciones", items: [
                { path: "/", label: "Inicio", icon: <LayoutDashboard size={20} /> },
                { path: "/pesadas", label: "Gestión de Pesadas", icon: <Scale size={20} /> },
                { path: "/ordenes-transporte", label: "Órdenes de Transporte", icon: <Truck size={20} /> },
                { path: "/planificacion", label: "Planificación Logística", icon: <FileText size={20} /> },
                { path: "/reportes", label: "Reportes & Métricas", icon: <BarChart2 size={20} /> },
            ]
        },
        {
            group: "Administración", items: [
                { path: "/vehiculos", label: "Vehículos", icon: <Truck size={20} /> },
                { path: "/conductores", label: "Conductores", icon: <Users size={20} /> },
                { path: "/productos", label: "Productos", icon: <Package size={20} /> },
                { path: "/razones-sociales", label: "Razones Sociales", icon: <Briefcase size={20} /> },
                { path: "/basculas", label: "Básculas", icon: <Monitor size={20} /> },
                { path: "/tanques", label: "Tanques", icon: <Database size={20} /> },
                { path: "/puestos", label: "Puestos de Expedición", icon: <MapPin size={20} /> },
                { path: "/dispositivos", label: "Dispositivos de Detección", icon: <Camera size={20} /> },
                { path: "/puntos-menu", label: "Puntos de Menú", icon: <ListChecks size={20} /> },
                { path: "/roles", label: "Roles y Permisos", icon: <Shield size={20} /> },
                { path: "/usuarios", label: "Usuarios", icon: <UserCheck size={20} /> },
            ]
        },
        {
            // Sin punto de menú: la ven todos los usuarios.
            group: "Ayuda", items: [
                { path: "/ayuda", label: "Ayuda y Capacitación", icon: <BookOpen size={20} /> },
            ]
        }
    ];

    // Solo las pantallas permitidas por los roles del usuario (Inicio no lleva punto: la ven todos).
    const menuVisible = menuItems
        .map(group => ({ ...group, items: group.items.filter(item => rutaPermitida(item.path, sesion?.puntosMenu)) }))
        .filter(group => group.items.length > 0);

    const usuario = sesion?.usuario;
    const iniciales = `${usuario?.nombre?.[0] ?? ''}${usuario?.apellido?.[0] ?? ''}`.toUpperCase();
    const rolesTexto = (sesion?.roles ?? []).map(r => r.descripcion).join(', ');

    const handleLogout = () => {
        logout();
        navigate('/login', { replace: true });
    };

    return (
        <div className="flex h-screen bg-slate-50 font-sans text-slate-900 overflow-hidden">
            {/* Sidebar */}
            <aside className="w-72 bg-linear-to-b from-slate-900 via-slate-900 to-blue-950 text-white flex flex-col shadow-2xl z-50">
                <div className="p-8 flex items-center gap-3">
                    <div className="w-12 h-12 bg-blue-600 rounded-xl flex items-center justify-center shadow-lg shadow-blue-500/20 rotate-3">
                        <Scale className="text-white" size={24} />
                    </div>
                    <div>
                        <h1 className="text-xl font-bold tracking-tight">CILP <span className="text-blue-400">Refinería</span></h1>
                        <p className="text-[10px] text-slate-400 font-medium uppercase tracking-[0.2em]">Smart Weighing System</p>
                    </div>
                </div>

                <nav className="flex-1 px-4 space-y-8 overflow-y-auto custom-scrollbar pt-2">
                    {menuVisible.map((group, idx) => (
                        <div key={idx}>
                            <p className="px-4 text-[11px] font-bold text-slate-500 uppercase tracking-[0.15em] mb-3">{group.group}</p>
                            <div className="space-y-1">
                                {group.items.map((item) => {
                                    const isActive = location.pathname === item.path;
                                    return (
                                        <Link
                                            key={item.path}
                                            to={item.path}
                                            className={`group flex items-center justify-between px-4 py-3 rounded-xl transition-all duration-300 ${isActive
                                                ? "bg-blue-600 text-white shadow-lg shadow-blue-600/20"
                                                : "text-slate-400 hover:text-white hover:bg-white/5"
                                                }`}
                                        >
                                            <div className="flex items-center gap-3">
                                                <span className={`${isActive ? "text-white" : "text-slate-500 group-hover:text-blue-400"} transition-colors`}>
                                                    {item.icon}
                                                </span>
                                                <span className="text-[14px] font-medium">{item.label}</span>
                                            </div>
                                            {isActive && <ChevronRight size={14} className="animate-pulse" />}
                                        </Link>
                                    );
                                })}
                            </div>
                        </div>
                    ))}
                </nav>

                <div className="p-6 mt-auto border-t border-white/5 bg-black/20">
                    <div className="flex items-center gap-3 mb-6">
                        <div className="w-10 h-10 shrink-0 rounded-full bg-linear-to-tr from-blue-600 to-blue-400 flex items-center justify-center font-bold text-sm">{iniciales}</div>
                        <div className="flex-1 min-w-0">
                            <p className="text-xs font-bold leading-none truncate" title={usuario?.idRed}>{usuario?.nombre} {usuario?.apellido}</p>
                            <p className="text-[10px] text-slate-500 mt-1 truncate" title={rolesTexto}>{rolesTexto}</p>
                        </div>
                        <button onClick={handleLogout} title="Cerrar sesión" className="text-slate-500 hover:text-red-400 transition-colors">
                            <LogOut size={16} />
                        </button>
                    </div>
                    <div className="flex items-center justify-between text-[10px] text-slate-600 font-mono">
                        <span>VERSION 0.9.0-BETA</span>
                        <span className="w-2 h-2 rounded-full bg-green-500 animate-pulse"></span>
                    </div>
                </div>
            </aside>

            {/* Main Content Area */}
            <div className="flex-1 flex flex-col min-w-0 bg-slate-50 relative overflow-hidden">
                {/* Top Header */}
                <header className="h-20 bg-white/80 backdrop-blur-md border-b border-slate-200 px-8 flex items-center justify-between sticky top-0 z-40">
                    <div className="flex-1"></div>
                    <div className="flex items-center gap-6">
                        <div className="flex items-center gap-3">
                            <div className="text-right">
                                <p className="text-sm font-bold leading-none">Planta La Plata</p>
                            </div>
                        </div>
                    </div>
                </header>

                {/* Content Scroll Area */}
                <main className="flex-1 overflow-y-auto p-8 custom-scrollbar">
                    <div className="max-w-7xl mx-auto animate-in fade-in duration-700">
                        <Outlet />
                    </div>
                </main>
            </div>
        </div>
    );
};

export default MainLayout;