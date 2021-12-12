create table games
(
	id bigint generated always as identity,
    status varchar(30) not null,
    objective text not null,
	winner int,
	maximum_bet bigint,
    primary key(id)
);

ALTER TABLE games OWNER TO drakoapi;

create table game_options
(
    id bigint generated always as identity,
    game_id bigint not null,
    odds varchar(20),
    description text not null,
    primary key(id),
    constraint fk_options_games foreign key(game_id) references games(id)
);

ALTER TABLE game_options OWNER TO drakoapi;

create table wagers
(
    id bigint generated always as identity,
    game_id bigint not null,
    user_id bigint not null,
    amount bigint not null,
    game_option_id bigint not null,
    primary key(id),
    constraint fk_wagers_games foreign key(game_id) references games(id),
    constraint fk_wagers_users foreign key(user_id) references users(id),
    constraint fk_wagers_options foreign key(game_option_id) references game_options(id),
    unique (game_id, user_id)
);

ALTER TABLE wagers OWNER TO drakoapi;
