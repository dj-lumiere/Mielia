package com.mielia.rider

import com.intellij.ide.FileIconProvider
import com.intellij.ide.plugins.PluginManagerCore
import com.intellij.openapi.extensions.PluginId
import com.intellij.openapi.project.Project
import com.intellij.openapi.util.IconLoader
import com.intellij.openapi.vfs.VirtualFile
import org.jetbrains.plugins.textmate.api.TextMateBundleProvider
import javax.swing.Icon

internal const val PLUGIN_ID = "com.mielia.rider"

private val EXTENSIONS = setOf("mi")

internal fun VirtualFile.isMielia(): Boolean = extension?.lowercase() in EXTENSIONS

/**
 * Registers the TextMate bundle shipped next to the plugin's jar: the grammar from Mielia.tmbundle plus the comment
 * and bracket rules of language-configuration.json. `.mi` files open as TextMate files, highlighted by that grammar.
 */
class MieliaBundleProvider : TextMateBundleProvider {
    override fun getBundles(): List<TextMateBundleProvider.PluginBundle> {
        val plugin = PluginManagerCore.getPlugin(PluginId.getId(PLUGIN_ID)) ?: return emptyList()
        return listOf(TextMateBundleProvider.PluginBundle("Mielia", plugin.pluginPath.resolve("bundle")))
    }
}

/** Gives `.mi` files the Mielia icon in place of the generic TextMate one. */
class MieliaIconProvider : FileIconProvider {
    override fun getIcon(file: VirtualFile, flags: Int, project: Project?): Icon? =
        if (file.isMielia()) ICON else null

    internal companion object {
        val ICON: Icon = IconLoader.getIcon("/icons/mielia.svg", MieliaIconProvider::class.java)
    }
}
