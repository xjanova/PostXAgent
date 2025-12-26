import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../data/models/post.dart';
import '../data/repositories/post_repository.dart';
import 'auth_provider.dart';

/// Post Repository Provider
final postRepositoryProvider = Provider<PostRepository>((ref) {
  final apiService = ref.watch(apiServiceProvider);
  return PostRepository(apiService);
});

/// Posts State
class PostsState {
  final List<Post> posts;
  final bool isLoading;
  final bool isLoadingMore;
  final String? error;
  final int currentPage;
  final bool hasMore;
  final String? statusFilter;
  final String? platformFilter;
  final String? searchQuery;

  const PostsState({
    this.posts = const [],
    this.isLoading = false,
    this.isLoadingMore = false,
    this.error,
    this.currentPage = 1,
    this.hasMore = true,
    this.statusFilter,
    this.platformFilter,
    this.searchQuery,
  });

  PostsState copyWith({
    List<Post>? posts,
    bool? isLoading,
    bool? isLoadingMore,
    String? error,
    int? currentPage,
    bool? hasMore,
    String? statusFilter,
    String? platformFilter,
    String? searchQuery,
  }) {
    return PostsState(
      posts: posts ?? this.posts,
      isLoading: isLoading ?? this.isLoading,
      isLoadingMore: isLoadingMore ?? this.isLoadingMore,
      error: error,
      currentPage: currentPage ?? this.currentPage,
      hasMore: hasMore ?? this.hasMore,
      statusFilter: statusFilter ?? this.statusFilter,
      platformFilter: platformFilter ?? this.platformFilter,
      searchQuery: searchQuery ?? this.searchQuery,
    );
  }
}

/// Posts Notifier
class PostsNotifier extends StateNotifier<PostsState> {
  final PostRepository _repository;

  PostsNotifier(this._repository) : super(const PostsState());

  /// Load initial posts
  Future<void> loadPosts({bool refresh = false}) async {
    if (state.isLoading) return;

    state = state.copyWith(
      isLoading: true,
      error: null,
      currentPage: refresh ? 1 : state.currentPage,
      posts: refresh ? [] : state.posts,
    );

    final result = await _repository.getPosts(
      page: 1,
      status: state.statusFilter,
      platform: state.platformFilter,
      search: state.searchQuery,
    );

    if (result.isSuccess) {
      state = state.copyWith(
        posts: result.data!.items,
        isLoading: false,
        currentPage: 1,
        hasMore: result.data!.hasMore,
      );
    } else {
      state = state.copyWith(
        isLoading: false,
        error: result.error,
      );
    }
  }

  /// Load more posts (pagination)
  Future<void> loadMore() async {
    if (state.isLoadingMore || !state.hasMore) return;

    state = state.copyWith(isLoadingMore: true);

    final result = await _repository.getPosts(
      page: state.currentPage + 1,
      status: state.statusFilter,
      platform: state.platformFilter,
      search: state.searchQuery,
    );

    if (result.isSuccess) {
      state = state.copyWith(
        posts: [...state.posts, ...result.data!.items],
        isLoadingMore: false,
        currentPage: state.currentPage + 1,
        hasMore: result.data!.hasMore,
      );
    } else {
      state = state.copyWith(
        isLoadingMore: false,
        error: result.error,
      );
    }
  }

  /// Set status filter
  Future<void> setStatusFilter(String? status) async {
    state = state.copyWith(statusFilter: status);
    await loadPosts(refresh: true);
  }

  /// Set platform filter
  Future<void> setPlatformFilter(String? platform) async {
    state = state.copyWith(platformFilter: platform);
    await loadPosts(refresh: true);
  }

  /// Search posts
  Future<void> search(String query) async {
    state = state.copyWith(searchQuery: query.isEmpty ? null : query);
    await loadPosts(refresh: true);
  }

  /// Clear filters
  Future<void> clearFilters() async {
    state = state.copyWith(
      statusFilter: null,
      platformFilter: null,
      searchQuery: null,
    );
    await loadPosts(refresh: true);
  }

  /// Delete post
  Future<bool> deletePost(int id) async {
    final result = await _repository.deletePost(id);
    if (result.isSuccess) {
      state = state.copyWith(
        posts: state.posts.where((p) => p.id != id).toList(),
      );
      return true;
    }
    return false;
  }

  /// Publish post
  Future<bool> publishPost(int id) async {
    final result = await _repository.publishPost(id);
    if (result.isSuccess) {
      state = state.copyWith(
        posts: state.posts.map((p) {
          if (p.id == id) return result.data!;
          return p;
        }).toList(),
      );
      return true;
    }
    return false;
  }
}

/// Posts Notifier Provider
final postsNotifierProvider =
    StateNotifierProvider<PostsNotifier, PostsState>((ref) {
  final repository = ref.watch(postRepositoryProvider);
  return PostsNotifier(repository);
});

/// Single Post State
class PostDetailState {
  final Post? post;
  final bool isLoading;
  final String? error;

  const PostDetailState({
    this.post,
    this.isLoading = false,
    this.error,
  });

  PostDetailState copyWith({
    Post? post,
    bool? isLoading,
    String? error,
  }) {
    return PostDetailState(
      post: post ?? this.post,
      isLoading: isLoading ?? this.isLoading,
      error: error,
    );
  }
}

/// Post Detail Notifier
class PostDetailNotifier extends StateNotifier<PostDetailState> {
  final PostRepository _repository;

  PostDetailNotifier(this._repository) : super(const PostDetailState());

  Future<void> loadPost(int id) async {
    state = state.copyWith(isLoading: true, error: null);

    final result = await _repository.getPost(id);

    if (result.isSuccess) {
      state = state.copyWith(post: result.data, isLoading: false);
    } else {
      state = state.copyWith(isLoading: false, error: result.error);
    }
  }

  void clear() {
    state = const PostDetailState();
  }
}

/// Post Detail Provider
final postDetailNotifierProvider =
    StateNotifierProvider<PostDetailNotifier, PostDetailState>((ref) {
  final repository = ref.watch(postRepositoryProvider);
  return PostDetailNotifier(repository);
});

/// Create Post State
class CreatePostState {
  final bool isLoading;
  final bool isGenerating;
  final String? error;
  final String? generatedContent;
  final List<String>? generatedHashtags;

  const CreatePostState({
    this.isLoading = false,
    this.isGenerating = false,
    this.error,
    this.generatedContent,
    this.generatedHashtags,
  });

  CreatePostState copyWith({
    bool? isLoading,
    bool? isGenerating,
    String? error,
    String? generatedContent,
    List<String>? generatedHashtags,
  }) {
    return CreatePostState(
      isLoading: isLoading ?? this.isLoading,
      isGenerating: isGenerating ?? this.isGenerating,
      error: error,
      generatedContent: generatedContent ?? this.generatedContent,
      generatedHashtags: generatedHashtags ?? this.generatedHashtags,
    );
  }
}

/// Create Post Notifier
class CreatePostNotifier extends StateNotifier<CreatePostState> {
  final PostRepository _repository;

  CreatePostNotifier(this._repository) : super(const CreatePostState());

  /// Create a new post
  Future<Post?> createPost(CreatePostRequest request) async {
    state = state.copyWith(isLoading: true, error: null);

    final result = await _repository.createPost(request);

    if (result.isSuccess) {
      state = state.copyWith(isLoading: false);
      return result.data;
    } else {
      state = state.copyWith(isLoading: false, error: result.error);
      return null;
    }
  }

  /// Generate AI content
  Future<void> generateContent(GenerateContentRequest request) async {
    state = state.copyWith(isGenerating: true, error: null);

    final result = await _repository.generateContent(request);

    if (result.isSuccess && result.data!.success) {
      state = state.copyWith(
        isGenerating: false,
        generatedContent: result.data!.content,
        generatedHashtags: result.data!.hashtags,
      );
    } else {
      state = state.copyWith(
        isGenerating: false,
        error: result.data?.message ?? result.error ?? 'ไม่สามารถสร้างเนื้อหาได้',
      );
    }
  }

  /// Clear generated content
  void clearGenerated() {
    state = state.copyWith(
      generatedContent: null,
      generatedHashtags: null,
    );
  }

  /// Clear all state
  void reset() {
    state = const CreatePostState();
  }
}

/// Create Post Provider
final createPostNotifierProvider =
    StateNotifierProvider<CreatePostNotifier, CreatePostState>((ref) {
  final repository = ref.watch(postRepositoryProvider);
  return CreatePostNotifier(repository);
});

/// Post Statistics Provider
final postStatsProvider = FutureProvider<Map<String, int>>((ref) async {
  final repository = ref.watch(postRepositoryProvider);
  final result = await repository.getPostStats();

  if (result.isSuccess) {
    return result.data!;
  }

  return {
    'total': 0,
    'draft': 0,
    'scheduled': 0,
    'published': 0,
    'failed': 0,
    'viral': 0,
  };
});
