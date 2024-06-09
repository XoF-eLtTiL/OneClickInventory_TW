﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using dog.miruku.inventory;
using nadena.dev.modular_avatar.core;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

public class MenuGenerator
{

    private static ModularAvatarMenuItem AddSubmenu(string name, Texture2D icon, Transform parent)
    {
        var menuObject = new GameObject(name);
        menuObject.transform.SetParent(parent);
        var menu = menuObject.AddComponent<ModularAvatarMenuItem>();

        menu.Control = new VRCExpressionsMenu.Control()
        {
            name = name,
            icon = icon,
            type = VRCExpressionsMenu.Control.ControlType.SubMenu,
            value = 0,
        };
        menu.MenuSource = SubmenuSource.Children;
        return menu;
    }

    private static ModularAvatarMenuItem AddToggleMenu(string name, Texture2D icon, string parameter, int value, Transform parent)
    {
        var menuObject = new GameObject(name);
        menuObject.transform.SetParent(parent);
        var menu = menuObject.AddComponent<ModularAvatarMenuItem>();

        menu.Control = new VRCExpressionsMenu.Control()
        {
            name = name,
            icon = icon,
            type = VRCExpressionsMenu.Control.ControlType.Toggle,
            parameter = new VRCExpressionsMenu.Control.Parameter()
            {
                name = parameter
            },
            value = value,
        };
        return menu;
    }

    private static ModularAvatarMenuItem CreateMAMenu(InventoryNode node, Transform parent)
    {
        var menuItemsToInstall = node.MenuItemsToInstall.ToArray();
        if (node.HasChildren || menuItemsToInstall.Length > 0)
        {
            var submenu = AddSubmenu(node.Value.Name, node.Value.Icon, parent);
            if (node.IsItem) AddToggleMenu(Localization.Get("enable"), node.Value.Icon, node.ParameterName, node.ParameterValue, submenu.transform);
            foreach (var child in node.Children)
            {
                CreateMAMenu(child, submenu.transform);
            }

            if (menuItemsToInstall.Length > 0)
            {
                foreach (var menuItem in menuItemsToInstall)
                {
                    menuItem.transform.SetParent(submenu.transform);
                }
            }
            return submenu;
        }
        else if (node.IsItem)
        {
            return AddToggleMenu(node.Value.Name, node.Value.Icon, node.ParameterName, node.ParameterValue, parent);
        }

        return AddSubmenu(node.Value.Name, node.Value.Icon, parent);
    }

    private static Dictionary<string, ParameterConfig> GetMAParameterConfigs(InventoryNode node, Dictionary<string, ParameterConfig> configs = null)
    {
        if (configs == null) configs = new Dictionary<string, ParameterConfig>();

        if (node.IsItem)
        {
            configs[node.ParameterName] = new ParameterConfig()
            {
                nameOrPrefix = node.ParameterName,
                syncType = ParameterSyncType.Int,
                defaultValue = 0,
                saved = false,
                localOnly = true,
            };

            configs[AnimationGenerator.GetSyncedParameterName(node.ParameterName)] = new ParameterConfig()
            {
                nameOrPrefix = AnimationGenerator.GetSyncedParameterName(node.ParameterName),
                syncType = ParameterSyncType.Bool,
                defaultValue = 0,
                saved = false,
                localOnly = true,
            };

            foreach (var (name, defaultValue) in AnimationGenerator.Encode(node.ParameterName, node.ParameterBits, node.ParameterDefault))
            {
                configs[name] =
                    new ParameterConfig()
                    {
                        nameOrPrefix = name,
                        syncType = ParameterSyncType.Bool,
                        defaultValue = defaultValue,
                        saved = true,
                        localOnly = false
                    };
            }
        }


        foreach (var child in node.Children) configs = GetMAParameterConfigs(child, configs);

        return configs;
    }

    private static void CreateMAParameters(InventoryNode node)
    {
        var parametersObject = new GameObject($"Parameters");
        parametersObject.transform.SetParent(node.Root.Value.transform, false);
        var parameters = parametersObject.AddComponent<ModularAvatarParameters>();
        var configs = GetMAParameterConfigs(node);
        parameters.parameters = configs.Values.ToList();
    }

    private static void CreateMAMergeAnimator(Dictionary<AnimatorController, int> controllers, GameObject parent)
    {
        // Add merge animator
        foreach (var entry in controllers)
        {
            var mergeAnimator = parent.AddComponent<ModularAvatarMergeAnimator>();
            mergeAnimator.animator = entry.Key;
            mergeAnimator.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
            mergeAnimator.deleteAttachedAnimator = true;
            mergeAnimator.pathMode = MergeAnimatorPathMode.Absolute;
            mergeAnimator.matchAvatarWriteDefaults = false;
            mergeAnimator.layerPriority = entry.Value;
        }
    }

    public static void Generate(InventoryNode node, Dictionary<AnimatorController, int> controllers, Transform menuParent)
    {
        var mergeAnimatorParent = new GameObject("MergeAnimator");
        mergeAnimatorParent.transform.SetParent(node.Root.Value.transform, false);
        CreateMAMergeAnimator(controllers, mergeAnimatorParent);
        CreateMAParameters(node);
        if (node.IsRoot && node.Value.InstallMenuInRoot)
        {
            var menuItem = CreateMAMenu(node, node.Avatar.transform);
            var installer = menuItem.gameObject.AddComponent<ModularAvatarMenuInstaller>();
            installer.menuToAppend = node.Avatar.expressionsMenu;
        }
        else
        {
            CreateMAMenu(node, menuParent);
        }
    }
}
