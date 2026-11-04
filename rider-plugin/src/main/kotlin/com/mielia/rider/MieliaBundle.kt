package com.mielia.rider

import com.intellij.DynamicBundle
import org.jetbrains.annotations.PropertyKey

private const val BUNDLE = "messages.MieliaBundle"

/** The plugin's text in the IDE's language (messages/MieliaBundle*.properties). */
internal object MieliaBundle : DynamicBundle(BUNDLE) {
    fun message(@PropertyKey(resourceBundle = BUNDLE) key: String, vararg params: Any): String = getMessage(key, *params)
}
